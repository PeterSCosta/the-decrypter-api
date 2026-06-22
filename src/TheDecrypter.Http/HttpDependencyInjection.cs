using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using TheDecrypter.Domain.Gateways;
using TheDecrypter.Http.Gateways;

namespace TheDecrypter.Http;

public static class HttpDependencyInjection
{
    /// <summary>
    /// Cliente HTTP tipado para CNPJ com resiliência Polly: timeout, retry,
    /// circuit-breaker e <b>rate-limiter por upstream</b> (ex.: ReceitaWS = 3/min).
    /// O limite é em memória; para multi-réplica use um token-bucket no Redis.
    /// </summary>
    public static IServiceCollection AddHttpDependencyInjection(
        this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["Gateways:Cnpj:BaseUrl"] ?? "https://brasilapi.com.br/api/";
        var ratePerMinute = configuration.GetValue<int?>("Gateways:Cnpj:RatePerMinute") ?? 3;

        services.AddHttpClient<ICnpjGateway, BrasilApiCnpjGateway>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(25);
        })
        .AddResilienceHandler("cnpj", builder =>
        {
            builder.AddTimeout(TimeSpan.FromSeconds(12));

            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            });

            builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 8,
                BreakDuration = TimeSpan.FromSeconds(15),
            });

            builder.AddRateLimiter(new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = ratePerMinute,
                TokensPerPeriod = ratePerMinute,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 200,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            }));
        });

        return services;
    }
}
