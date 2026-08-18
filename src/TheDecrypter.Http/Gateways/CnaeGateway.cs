using System.Text.Json;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Http.Gateways;

/// <summary>
/// CNAE pela API do IBGE — o MESMO host que já serve os municípios, sem chave.
///
/// ── A ARMADILHA, MEDIDA ─────────────────────────────────────────────────────
/// Código inexistente NÃO devolve 404: devolve **HTTP 200 com `[]`**. E código
/// existente devolve um OBJETO. Ou seja, a forma do JSON muda com o resultado —
/// desserializar direto num tipo estoura no dia em que alguém digitar um número
/// errado, que é justamente o dia mais provável. Por isso a checagem de
/// `ValueKind` antes de qualquer leitura.
/// </summary>
public class CnaeGateway(HttpClient http) : ICnaeGateway
{
    public async Task<CnaeInfo?> GetCnaeAsync(string codigo, CancellationToken ct = default)
    {
        var limpo = new string([.. codigo.Where(char.IsDigit)]);
        if (limpo.Length != 7) return null;

        using var resp = await http.GetAsync(
            $"api/v2/cnae/subclasses/{limpo}", HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        var r = doc.RootElement;

        // `[]` = não existe. É este o caminho do "não encontrei" nesta API.
        if (r.ValueKind == JsonValueKind.Array)
        {
            if (r.GetArrayLength() == 0) return null;
            r = r[0];
        }
        if (r.ValueKind != JsonValueKind.Object) return null;

        static string? Texto(JsonElement e, string prop) =>
            e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
        static JsonElement? Filho(JsonElement e, string prop) =>
            e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

        var descricao = Texto(r, "descricao") ?? "";
        var classe = Filho(r, "classe");
        var grupo = classe is { } c ? Filho(c, "grupo") : null;
        var divisao = grupo is { } g ? Filho(g, "divisao") : null;
        var secao = divisao is { } d ? Filho(d, "secao") : null;

        return new CnaeInfo(
            limpo,
            Formatar(limpo),
            descricao,
            classe is { } c2 ? Texto(c2, "descricao") : null,
            grupo is { } g2 ? Texto(g2, "descricao") : null,
            divisao is { } d2 ? Texto(d2, "descricao") : null,
            secao is { } s2 ? Texto(s2, "id") : null,
            secao is { } s3 ? Texto(s3, "descricao") : null);
    }

    /// <summary>`6201501` → `62.01-5/01`, que é como o código aparece impresso.</summary>
    public static string Formatar(string sete) =>
        sete.Length == 7
            ? $"{sete[..2]}.{sete[2..4]}-{sete[4]}/{sete[5..]}"
            : sete;
}
