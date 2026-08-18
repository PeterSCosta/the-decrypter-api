using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using TheDecrypter.Api.Auth;
using TheDecrypter.Application;
using TheDecrypter.Application.Auth;
using TheDecrypter.Cache;
using TheDecrypter.Domain.Http;
using TheDecrypter.Ef;
using TheDecrypter.Http;

var builder = WebApplication.CreateBuilder(args);

// Compressão Brotli + Gzip (respostas JSON podem ser grandes: cep/search, pix).
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Output cache: respostas 200 dos lookups guardadas em memória 10 min (varia por
// rota/query). Camada acima do cache de dados (Redis): repete sem refazer nada.
builder.Services.AddOutputCache(o =>
{
    o.AddPolicy("lookups", b => b.Expire(TimeSpan.FromMinutes(10)));
    // Frota muda o tempo todo: cache curto só p/ coalescer o polling do front (~15s).
    o.AddPolicy("fleet", b => b.Expire(TimeSpan.FromSeconds(15)));
});

// Rate-limit por IP (protege a NOSSA API; separado do limite por-upstream do Polly).
//
// Duas faixas na MESMA partição global, e não uma política de endpoint: em
// ASP.NET Core o `GlobalLimiter` roda **junto** com as políticas de endpoint, não
// no lugar delas — um `[EnableRateLimiting]` mais generoso não levantaria o teto,
// só somaria outro. Então a distinção mora aqui.
//
// Nota: a partição é por IP, e uma equipe inteira atrás do NAT do local divide o
// mesmo balde. Por isso o limite de login é por IP+e-mail: senão um endereço
// compartilhado travaria o login de todo mundo ao primeiro engano de senha.
// De quem é a requisição. NÃO usar `Connection.RemoteIpAddress` direto: atrás da
// Cloudflare ele é o IP da borda, que muda a cada requisição — e uma partição
// nova por requisição é um limitador desligado. Ver `ClientIp`.
static string DeQuemE(HttpContext ctx) => ClientIp.Resolver(
    ctx.Request.Headers[ClientIp.CabecalhoCloudflare],
    ctx.Request.Headers["X-Forwarded-For"],
    ctx.Connection.RemoteIpAddress?.ToString());

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            DeQuemE(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));

    // Login e cadastro: 120/min é convite a força bruta de senha.
    o.AddPolicy("auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"auth:{DeQuemE(ctx)}",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    // O 429 chegava com corpo vazio, e o app o mostrava como "erro inesperado".
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"message\":\"Muitas tentativas. Aguarde um minuto.\"}", ct);
    };
});

// Atrás do Traefik/Dokploy: usar o IP real do cliente (X-Forwarded-For).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Camadas (espelham o the-logic-lab): Cache(Redis) · Ef(Postgres) · Http(Polly) · Application
builder.Services.AddCacheDependencyInjection(builder.Configuration);
builder.Services.AddEfDependencyInjection(builder.Configuration);
builder.Services.AddHttpDependencyInjection(builder.Configuration);
builder.Services.AddApplicationDependencyInjection();

// ---- Autenticação (JWT Bearer) -------------------------------------------
//
// Bearer e não cookie: o CORS já libera qualquer header, então o `Authorization`
// passa sem mudança nenhuma de política. Cookie exigiria `AllowCredentials()`
// aqui e `credentials: "include"` em todo call site do app.
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (!args.Contains("seed"))
{
    // A validação é no BOOT, não no primeiro login. Uma chave curta só estoura
    // quando alguém tenta entrar — a API fica "saudável" e o login inteiro cai,
    // que é exatamente como isso passou despercebido num projeto irmão.
    if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < JwtOptions.MinimoDeBytes)
    {
        Console.Error.WriteLine(
            $"ERRO: Jwt__SigningKey precisa de pelo menos {JwtOptions.MinimoDeBytes} bytes " +
            $"(tem {Encoding.UTF8.GetByteCount(jwt.SigningKey)}). Gere uma com " +
            "`openssl rand -base64 48` e defina no ambiente. A API não sobe sem isso.");
        return 1;
    }
}
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            // Sem a tolerância padrão de 5 min: o token já é curto.
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Modo seed: `dotnet run -- seed --data <dir>` popula o Postgres e sai.
if (args.Contains("seed"))
{
    // Default container-friendly: o Dockerfile copia os JSONs para /app/data.
    // Dev local que quiser semear contra o PG dev:
    //   dotnet run -- seed --data ../the-decrypter/public/data
    var dataDir = GetArgValue(args, "--data") ?? "/app/data";
    if (!Directory.Exists(dataDir))
    {
        Console.Error.WriteLine(
            $"ERRO: --data aponta para '{dataDir}' que não existe. " +
            "Em dev, passe --data ../the-decrypter/public/data. " +
            "Em container, o dataset é embutido em /app/data pelo Dockerfile.");
        return 1;
    }
    using var scope = app.Services.CreateScope();
    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");
    await TheDecrypter.Api.Seed.Seeder.RunAsync(scope.ServiceProvider, dataDir, log);
    return 0;
}

// Outermost: transforma falha transitória de upstream em 503 (não 500).
app.UseMiddleware<TheDecrypter.Api.Middleware.UpstreamErrorMiddleware>();
app.UseForwardedHeaders();
app.UseResponseCompression();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseRateLimiter();
// Autenticação ANTES do output cache: uma resposta 401 não pode ser guardada e
// servida depois para quem tem token válido.
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();
app.MapControllers();

// Admin inicial. Sem ele, uma base zerada tranca: todo cadastro nasce pendente
// e só um admin aprova. Idempotente — não mexe em quem já existe.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IAuthService>().GarantirAdminInicialAsync(
        app.Configuration["Admin:Email"],
        app.Configuration["Admin:Senha"],
        // Opcional: quem já tem `Admin__Email` em produção não precisa fazer
        // nada. Definir o apelido depois só ACRESCENTA — a conta é achada pelo
        // e-mail e ganha o apelido, sem virar uma segunda conta de admin.
        app.Configuration["Admin:Apelido"]);
}

app.Run();
return 0;

static string? GetArgValue(string[] arguments, string name)
{
    var i = Array.IndexOf(arguments, name);
    return i >= 0 && i + 1 < arguments.Length ? arguments[i + 1] : null;
}
