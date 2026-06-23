using System.IO.Compression;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using TheDecrypter.Application;
using TheDecrypter.Cache;
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
    o.AddPolicy("lookups", b => b.Expire(TimeSpan.FromMinutes(10))));

// Rate-limit por IP (protege a NOSSA API; separado do limite por-upstream do Polly).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

// Atrás do Traefik/Dokploy: usar o IP real do cliente (X-Forwarded-For).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// Camadas (espelham o the-logic-lab): Cache(Redis) · Ef(Postgres) · Http(Polly) · Application
builder.Services.AddCacheDependencyInjection(builder.Configuration);
builder.Services.AddEfDependencyInjection(builder.Configuration);
builder.Services.AddHttpDependencyInjection(builder.Configuration);
builder.Services.AddApplicationDependencyInjection();

var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Modo seed: `dotnet run -- seed --data <dir>` popula o Postgres e sai.
if (args.Contains("seed"))
{
    var dataDir = GetArgValue(args, "--data") ?? "../the-decrypter/public/data";
    using var scope = app.Services.CreateScope();
    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");
    await TheDecrypter.Api.Seed.Seeder.RunAsync(scope.ServiceProvider, dataDir, log);
    return;
}

app.UseForwardedHeaders();
app.UseResponseCompression();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseRateLimiter();
app.UseOutputCache();
app.MapControllers();

app.Run();

static string? GetArgValue(string[] arguments, string name)
{
    var i = Array.IndexOf(arguments, name);
    return i >= 0 && i + 1 < arguments.Length ? arguments[i + 1] : null;
}
