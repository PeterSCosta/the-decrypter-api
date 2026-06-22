using TheDecrypter.Domain.Gateways;
using TheDecrypter.Domain.Services.Cache;

namespace TheDecrypter.Application.Cnpj;

public interface ICnpjService
{
    Task<CnpjInfo?> LookupAsync(string cnpj, CancellationToken ct = default);
}

/// <summary>Consulta de CNPJ <b>cache-first</b>: Redis primeiro; só vai ao
/// provedor externo (rate-limited) em caso de miss, e então cacheia.</summary>
public class CnpjService(ICacheService cache, ICnpjGateway gateway) : ICnpjService
{
    private const int TtlMinutes = 60 * 24 * 7;  // 7 dias (CNPJ muda pouco)
    private const int SlidingMinutes = 60 * 24;  // 1 dia

    public async Task<CnpjInfo?> LookupAsync(string cnpj, CancellationToken ct = default)
    {
        var digits = new string(cnpj.Where(char.IsDigit).ToArray());
        if (digits.Length != 14) return null;

        var key = $"cnpj:{digits}";
        var cached = await cache.Get<CnpjInfo>(key);
        if (cached is not null) return cached;

        var info = await gateway.GetAsync(digits, ct);
        if (info is not null) await cache.Set(key, info, TtlMinutes, SlidingMinutes);
        return info;
    }
}
