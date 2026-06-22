using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheDecrypter.Domain.Services.Cache;

namespace TheDecrypter.Cache.Service;

/// <summary>Cache distribuído sobre Redis. Espelha o CacheService do the-logic-lab.</summary>
public class CacheService(
    IDistributedCache distributedCache,
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<CacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    public async Task Remove(string key) => await distributedCache.RemoveAsync(key);

    public async Task<T?> Get<T>(string key)
    {
        try
        {
            var value = await distributedCache.GetStringAsync(key);
            return value is null ? default : JsonSerializer.Deserialize<T>(value, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error on get cache key {key}", key);
            return default;
        }
    }

    public async Task<T> Set<T>(string key, T value, int expirationMinutes, int expirationSlidingMinutes)
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expirationMinutes),
                SlidingExpiration = TimeSpan.FromMinutes(expirationSlidingMinutes),
            };
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await distributedCache.SetStringAsync(key, json, options);
            return value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error on set cache key {key}", key);
            return value;
        }
    }

    public async Task<IEnumerable<string>> GetKeyFromPrefix(string prefix)
    {
        var keys = new List<string>();
        try
        {
            foreach (var endpoint in connectionMultiplexer.GetEndPoints())
            {
                var server = connectionMultiplexer.GetServer(endpoint);
                await foreach (var key in server.KeysAsync(pattern: $"*{prefix}*"))
                    keys.Add(key.ToString());
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error on get keys from prefix {prefix}", prefix);
        }
        return keys;
    }
}
