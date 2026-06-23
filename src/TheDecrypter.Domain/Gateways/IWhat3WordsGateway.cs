namespace TheDecrypter.Domain.Gateways;

/// <summary>what3words → coordenada. A chave fica no servidor (escondida do front).
/// Retorna null se a chave não estiver configurada ou o endereço for inválido.</summary>
public interface IWhat3WordsGateway
{
    Task<W3wInfo?> ConvertAsync(string words, CancellationToken ct = default);
}
