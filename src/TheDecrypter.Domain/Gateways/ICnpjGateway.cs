namespace TheDecrypter.Domain.Gateways;

/// <summary>Provedor externo de consulta de CNPJ (BrasilAPI, ReceitaWS, …).</summary>
public interface ICnpjGateway
{
    /// <returns>Os dados do CNPJ, ou null se não encontrado.</returns>
    Task<CnpjInfo?> GetAsync(string cnpj, CancellationToken ct = default);
}
