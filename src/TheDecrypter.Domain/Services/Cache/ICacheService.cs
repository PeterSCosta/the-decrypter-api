namespace TheDecrypter.Domain.Services.Cache;

/// <summary>Abstração de cache distribuído (Redis). Mesmo contrato do the-logic-lab.</summary>
public interface ICacheService
{
    Task Remove(string key);
    Task<T?> Get<T>(string key);
    Task<T> Set<T>(string key, T value, int expirationMinutes, int expirationSlidingMinutes);
    Task<IEnumerable<string>> GetKeyFromPrefix(string prefix);
}
