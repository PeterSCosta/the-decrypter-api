namespace TheDecrypter.Domain.Gateways;

/// <summary>Dados de um CNPJ (normalizados, independentes do provedor).</summary>
public record CnpjInfo(
    string Cnpj,
    string? RazaoSocial,
    string? NomeFantasia,
    string? Situacao,
    string? Logradouro,
    string? Numero,
    string? Bairro,
    string? Municipio,
    string? Uf,
    string? Cep,
    string? AtividadePrincipal);
