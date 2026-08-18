using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Domain.Repositories;

public interface ILoteBlumenauRepository
{
    /// <summary>Aceita as duas grafias — 15 dígitos ou grupos com hífen.</summary>
    Task<LoteBlumenau?> ByInscricaoAsync(string entrada, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
