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
    /// Clientes HTTP tipados (CNPJ, BrasilAPI geral, Open Food Facts) com resiliência
    /// Polly: timeout, retry, circuit-breaker e <b>rate-limiter por upstream</b>
    /// (ex.: ReceitaWS = 3/min). Em memória; multi-réplica → token-bucket no Redis.
    /// </summary>
    public static IServiceCollection AddHttpDependencyInjection(
        this IServiceCollection services, IConfiguration configuration)
    {
        var brasilApi = configuration["Gateways:BrasilApi:BaseUrl"] ?? "https://brasilapi.com.br/api/";
        var off = configuration["Gateways:OpenFoodFacts:BaseUrl"] ?? "https://world.openfoodfacts.org/";
        var cnpjRate = configuration.GetValue<int?>("Gateways:Cnpj:RatePerMinute") ?? 3;
        var generalRate = configuration.GetValue<int?>("Gateways:BrasilApi:RatePerMinute") ?? 120;

        services.AddHttpClient<ICnpjGateway, BrasilApiCnpjGateway>(c => Configure(c, brasilApi))
            .AddResilienceHandler("cnpj", b => AddResilience(b, cnpjRate));

        services.AddHttpClient<IBrasilApiGateway, BrasilApiGateway>(c => Configure(c, brasilApi))
            .AddResilienceHandler("brasilapi", b => AddResilience(b, generalRate));

        services.AddHttpClient<IProductGateway, OpenFoodFactsGateway>(c => Configure(c, off))
            .AddResilienceHandler("off", b => AddResilience(b, generalRate));

        return services;

        static void Configure(HttpClient c, string baseUrl)
        {
            c.BaseAddress = new Uri(baseUrl);
            c.Timeout = TimeSpan.FromSeconds(25);
        }

        static void AddResilience(ResiliencePipelineBuilder<HttpResponseMessage> b, int ratePerMinute)
        {
            b.AddTimeout(TimeSpan.FromSeconds(12));
            b.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            });
            b.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 8,
                BreakDuration = TimeSpan.FromSeconds(15),
            });
            b.AddRateLimiter(new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = ratePerMinute,
                TokensPerPeriod = ratePerMinute,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 200,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            }));
        }
    }
}
