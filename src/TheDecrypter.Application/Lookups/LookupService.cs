using System.Text.RegularExpressions;
using TheDecrypter.Domain.Gateways;
using TheDecrypter.Domain.Repositories;
using TheDecrypter.Domain.Services.Cache;

namespace TheDecrypter.Application.Lookups;

public interface ILookupService
{
    Task<IsbnInfo?> IsbnAsync(string isbn, CancellationToken ct = default);
    Task<NcmInfo?> NcmAsync(string code, CancellationToken ct = default);
    Task<RegistroBrInfo?> RegistroBrAsync(string domain, CancellationToken ct = default);
    Task<ProductInfo?> ProductAsync(string barcode, CancellationToken ct = default);
    Task<PixParticipant?> PixAsync(string ispb, CancellationToken ct = default);
    Task<IReadOnlyList<PixParticipant>> PixListAsync(CancellationToken ct = default);
    Task<CepInfo?> CepAsync(string cep, CancellationToken ct = default);
    Task<W3wInfo?> W3wAsync(string words, CancellationToken ct = default);
    Task<GeocodeInfo?> GeocodeAsync(string query, CancellationToken ct = default);
}

/// <summary>Consultas externas cache-first (Redis → provedor → cacheia).</summary>
public partial class LookupService(
    ICacheService cache,
    IBrasilApiGateway brasil,
    IProductGateway products,
    ICepRepository cepRepo,
    IWhat3WordsGateway w3wGateway,
    IGeocodeGateway geoGateway) : ILookupService
{
    private const int Week = 60 * 24 * 7;
    private const int Day = 60 * 24;

    private static string Digits(string s) => new(s.Where(char.IsDigit).ToArray());

    private async Task<T?> CacheFirst<T>(string key, Func<Task<T?>> load, int ttl) where T : class
    {
        var hit = await cache.Get<T>(key);
        if (hit is not null) return hit;
        var val = await load();
        if (val is not null) await cache.Set(key, val, ttl, Day);
        return val;
    }

    public Task<IsbnInfo?> IsbnAsync(string isbn, CancellationToken ct = default)
    {
        var clean = new string(isbn.Where(c => char.IsDigit(c) || c is 'X' or 'x').ToArray());
        if (clean.Length is not (10 or 13)) return Task.FromResult<IsbnInfo?>(null);
        return CacheFirst($"isbn:{clean}", () => brasil.GetIsbnAsync(clean, ct), Week);
    }

    public Task<NcmInfo?> NcmAsync(string code, CancellationToken ct = default)
    {
        var clean = Digits(code);
        if (clean.Length is < 6 or > 8) return Task.FromResult<NcmInfo?>(null);
        return CacheFirst($"ncm:{clean}", () => brasil.GetNcmAsync(clean, ct), Week);
    }

    public Task<RegistroBrInfo?> RegistroBrAsync(string domain, CancellationToken ct = default)
    {
        var d = domain.Trim().ToLowerInvariant();
        if (!DomainBr().IsMatch(d)) return Task.FromResult<RegistroBrInfo?>(null);
        return CacheFirst($"registrobr:{d}", () => brasil.GetRegistroBrAsync(d, ct), Day);
    }

    public Task<ProductInfo?> ProductAsync(string barcode, CancellationToken ct = default)
    {
        var clean = Digits(barcode);
        if (clean.Length is < 8 or > 14) return Task.FromResult<ProductInfo?>(null);
        return CacheFirst($"produto:{clean}", () => products.GetProductAsync(clean, ct), Week);
    }

    private async Task<List<PixParticipant>> GetPixListAsync(CancellationToken ct)
    {
        var list = await cache.Get<List<PixParticipant>>("pix:list");
        if (list is null)
        {
            list = (await brasil.GetPixParticipantsAsync(ct)).ToList();
            if (list.Count > 0) await cache.Set("pix:list", list, Week, Day);
        }
        return list;
    }

    public async Task<PixParticipant?> PixAsync(string ispb, CancellationToken ct = default)
    {
        if (!Regex.IsMatch(ispb, @"^\d{8}$")) return null;
        return (await GetPixListAsync(ct)).FirstOrDefault(p => p.Ispb == ispb);
    }

    // Lista completa (~900) para o front indexar client-side; cacheada em Redis.
    public async Task<IReadOnlyList<PixParticipant>> PixListAsync(CancellationToken ct = default)
        => await GetPixListAsync(ct);

    public Task<CepInfo?> CepAsync(string cep, CancellationToken ct = default)
    {
        var digits = Digits(cep);
        if (digits.Length != 8) return Task.FromResult<CepInfo?>(null);
        return CacheFirst($"cep:{digits}", async () =>
        {
            var row = await cepRepo.GetByCodeAsync(digits, ct);
            if (row is not null)
                return new CepInfo(row.Code, row.Logradouro, row.Bairro, row.Localidade, row.Uf, row.Lat, row.Lng, "db");
            return await brasil.GetCepAsync(digits, ct);
        }, Week);
    }

    public Task<W3wInfo?> W3wAsync(string words, CancellationToken ct = default)
    {
        var w = words.Trim().TrimStart('/').ToLowerInvariant();
        if (!ThreeWords().IsMatch(w)) return Task.FromResult<W3wInfo?>(null);
        return CacheFirst($"w3w:{w}", () => w3wGateway.ConvertAsync(w, ct), Week);
    }

    public Task<GeocodeInfo?> GeocodeAsync(string query, CancellationToken ct = default)
    {
        var q = query.Trim();
        if (q.Length < 3) return Task.FromResult<GeocodeInfo?>(null);
        return CacheFirst($"geo:{q.ToLowerInvariant()}", () => geoGateway.SearchAsync(q, ct), Week);
    }

    [GeneratedRegex(@"^(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+br$")]
    private static partial Regex DomainBr();

    [GeneratedRegex(@"^\p{L}{2,}\.\p{L}{2,}\.\p{L}{2,}$")]
    private static partial Regex ThreeWords();
}
