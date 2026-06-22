using System.Text.Json.Serialization;
using TheDecrypter.Application;
using TheDecrypter.Cache;
using TheDecrypter.Ef;
using TheDecrypter.Http;

var builder = WebApplication.CreateBuilder(args);

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

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();

app.Run();
