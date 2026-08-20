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
        var w3w = configuration["Gateways:What3Words:BaseUrl"] ?? "https://api.what3words.com/";
        var nominatim = configuration["Gateways:Nominatim:BaseUrl"] ?? "https://nominatim.openstreetmap.org/";
        // Mesmo host que já serve os municípios do IBGE. Sem chave, com CORS —
        // mas passa por aqui do mesmo jeito, para herdar cache e limite.
        var ibge = configuration["Gateways:Ibge:BaseUrl"] ?? "https://servicodados.ibge.gov.br/";
        var userAgent = configuration["Gateways:Nominatim:UserAgent"]
            ?? "TheDecrypter/1.0 (+https://arromba.thelogiclab.com.br)";
        var cnpjRate = configuration.GetValue<int?>("Gateways:Cnpj:RatePerMinute") ?? 3;
        var generalRate = configuration.GetValue<int?>("Gateways:BrasilApi:RatePerMinute") ?? 120;

        services.AddHttpClient<ICnpjGateway, BrasilApiCnpjGateway>(c => Configure(c, brasilApi))
            .AddResilienceHandler("cnpj", b => AddResilience(b, cnpjRate));

        services.AddHttpClient<IBrasilApiGateway, BrasilApiGateway>(c => Configure(c, brasilApi))
            .AddResilienceHandler("brasilapi", b => AddResilience(b, generalRate));

        services.AddHttpClient<IProductGateway, OpenFoodFactsGateway>(c => Configure(c, off))
            .AddResilienceHandler("off", b => AddResilience(b, generalRate));

        services.AddHttpClient<IWhat3WordsGateway, What3WordsGateway>(c => Configure(c, w3w))
            .AddResilienceHandler("w3w", b => AddResilience(b, generalRate));

        services.AddHttpClient<ICnaeGateway, CnaeGateway>(c => Configure(c, ibge))
            .AddResilienceHandler("cnae", b => AddResilience(b, generalRate));

        // Wikidata: sem chave e sem cota, mas com POLÍTICA DE USO — o endpoint
        // pede User-Agent identificável, como o Nominatim, e por isso reusa o
        // mesmo. Consulta SPARQL é mais pesada que um GET de tabela; teto
        // próprio e mais baixo, e timeout maior que o padrão.
        services.AddHttpClient<IWikidataGateway, WikidataGateway>(c =>
        {
            Configure(c, configuration["Gateways:Wikidata:BaseUrl"] ?? "https://query.wikidata.org/");
            c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            c.DefaultRequestHeaders.Accept.ParseAdd("application/sparql-results+json");
            c.Timeout = TimeSpan.FromSeconds(20);
        }).AddResilienceHandler("wikidata", b => AddResilience(b, 30));

        // Timeout maior que os demais: aqui sobem centenas de KB de áudio, e o
        // reconhecimento do outro lado não é instantâneo.
        services.AddHttpClient<IMusicGateway, AuddGateway>(c =>
        {
            Configure(c, configuration["Gateways:Audd:BaseUrl"] ?? "https://api.audd.io/");
            c.Timeout = TimeSpan.FromSeconds(45);
        }).AddResilienceHandler("audd", b => AddResilience(b, generalRate));

        services.AddHttpClient<IGeocodeGateway, NominatimGateway>(c =>
        {
            Configure(c, nominatim);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent); // política do Nominatim
        }).AddResilienceHandler("nominatim", b => AddResilience(b, 30)); // ~1 req/s

        // Traccar (frota, self-hosted). Token/URL server-side; sem config o gateway
        // retorna vazio. Bearer setado no header quando há token.
        var traccar = configuration["Traccar:BaseUrl"];
        var traccarToken = configuration["Traccar:Token"];
        services.AddHttpClient<IFleetGateway, TraccarGateway>(c =>
        {
            if (!string.IsNullOrWhiteSpace(traccar))
                c.BaseAddress = new Uri(traccar.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(15);
            if (!string.IsNullOrWhiteSpace(traccarToken))
                c.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", traccarToken);
        }).AddResilienceHandler("traccar", b => AddResilience(b, 120));

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
