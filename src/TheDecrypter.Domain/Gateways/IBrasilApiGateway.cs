namespace TheDecrypter.Domain.Gateways;

/// <summary>Consultas no BrasilAPI (grátis, sem chave). 404 → null nas pontuais.</summary>
public interface IBrasilApiGateway
{
    Task<IsbnInfo?> GetIsbnAsync(string isbn, CancellationToken ct = default);
    Task<NcmInfo?> GetNcmAsync(string code, CancellationToken ct = default);
    Task<RegistroBrInfo?> GetRegistroBrAsync(string domain, CancellationToken ct = default);
    Task<CepInfo?> GetCepAsync(string cep, CancellationToken ct = default);
    Task<IReadOnlyList<PixParticipant>> GetPixParticipantsAsync(CancellationToken ct = default);
}
