using System.IO.Compression;
using System.Text.Json.Serialization;
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

app.UseResponseCompression();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.MapControllers();

app.Run();

static string? GetArgValue(string[] arguments, string name)
{
    var i = Array.IndexOf(arguments, name);
    return i >= 0 && i + 1 < arguments.Length ? arguments[i + 1] : null;
}
