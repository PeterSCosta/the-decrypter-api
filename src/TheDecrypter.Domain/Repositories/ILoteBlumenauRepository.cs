using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Domain.Repositories;

public interface ILoteBlumenauRepository
{
    /// <summary>
    /// Aceita as quatro grafias — 15 dígitos, 12 dígitos, grupos separados ou o
    /// IQ colado. Devolve LISTA porque a última delas é ambígua por natureza:
    /// sem os hífens, `41101634` é tanto `4-1-10-16-34` quanto `4-1-10-1-634`,
    /// e as duas existem. Escolher uma por conta própria seria dar resposta
    /// errada com cara de certa.
    /// </summary>
    Task<IReadOnlyList<LoteBlumenau>> BuscarAsync(string entrada, int limite, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);
}
