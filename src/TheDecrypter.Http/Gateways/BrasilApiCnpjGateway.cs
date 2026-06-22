using System.Net;
using System.Text.Json;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Http.Gateways;

/// <summary>CNPJ via BrasilAPI (grátis, sem chave). Para trocar por ReceitaWS,
/// basta outra implementação de ICnpjGateway com o mesmo HttpClient resiliente.</summary>
public class BrasilApiCnpjGateway(HttpClient http) : ICnpjGateway
{
    public async Task<CnpjInfo?> GetAsync(string cnpj, CancellationToken ct = default)
    {
        using var resp = await http.GetAsync($"cnpj/v1/{cnpj}", ct);
        if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            return null;
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var r = doc.RootElement;

        string? S(string prop) =>
            r.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        return new CnpjInfo(
            Cnpj: cnpj,
            RazaoSocial: S("razao_social"),
            NomeFantasia: S("nome_fantasia"),
            Situacao: S("descricao_situacao_cadastral"),
            Logradouro: S("logradouro"),
            Numero: S("numero"),
            Bairro: S("bairro"),
            Municipio: S("municipio"),
            Uf: S("uf"),
            Cep: S("cep"),
            AtividadePrincipal: S("cnae_fiscal_descricao"));
    }
}
