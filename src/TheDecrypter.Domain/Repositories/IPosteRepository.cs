using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Domain.Repositories;

public interface IPosteRepository
{
    /// <summary>Plaqueta exata. Comparação de string: "0338" ≠ "338".</summary>
    Task<Poste?> ByPlaquetaAsync(string plaqueta, CancellationToken ct = default);

    /// <summary>Plaqueta por prefixo, ou rua/bairro por nome sem acento.</summary>
    Task<IReadOnlyList<Poste>> SearchAsync(string q, int limit, CancellationToken ct = default);

    /// <summary>Os `limit` mais próximos, com a distância em metros.</summary>
    Task<IReadOnlyList<Poste>> NearAsync(double lat, double lng, int limit, CancellationToken ct = default);

    /// <summary>Dentro da caixa. `Truncado` avisa que o teto foi atingido.</summary>
    Task<(IReadOnlyList<Poste> Hits, bool Truncado)> BboxAsync(
        double sul, double norte, double oeste, double leste, int limit, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);
}
