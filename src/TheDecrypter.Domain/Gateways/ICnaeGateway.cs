namespace TheDecrypter.Domain.Gateways;

/// <summary>
/// Uma subclasse da CNAE, com a hierarquia inteira — que é o que interessa numa
/// prova: a atividade sozinha diz pouco, e a seção ("Indústrias de
/// transformação") é o agrupamento que costuma ser a pista.
/// </summary>
public record CnaeInfo(
    string Codigo,
    string CodigoFormatado,
    string Descricao,
    string? Classe,
    string? Grupo,
    string? Divisao,
    string? Secao,
    string? SecaoDescricao);

public interface ICnaeGateway
{
    /// <summary>Subclasse de 7 dígitos, já sem pontuação.</summary>
    Task<CnaeInfo?> GetCnaeAsync(string codigo, CancellationToken ct = default);
}
