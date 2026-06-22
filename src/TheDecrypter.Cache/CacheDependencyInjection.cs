using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TheDecrypter.Cache.Service;
using TheDecrypter.Domain.Exceptions;
using TheDecrypter.Domain.Services.Cache;

namespace TheDecrypter.Cache;

public static class CacheDependencyInjection
{
    public static IServiceCollection AddCacheDependencyInjection(
        this IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetConnectionString("Redis")
            ?? throw new NotFoundException("Redis connection not found");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redis);
            options.AbortOnConnectFail = false; // não derruba o app se o Redis estiver fora
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redis;
            options.InstanceName = "Decrypter_";
        });

        services.AddScoped<ICacheService, CacheService>();
        return services;
    }
}
