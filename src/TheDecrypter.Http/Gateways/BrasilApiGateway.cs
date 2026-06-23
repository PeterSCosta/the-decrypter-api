using System.Globalization;
using System.Net;
using System.Text.Json;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Http.Gateways;

/// <summary>ISBN, NCM, Registro.br, CEP e participantes do PIX via BrasilAPI.</summary>
public class BrasilApiGateway(HttpClient http) : IBrasilApiGateway
{
    private async Task<JsonElement?> GetOrNull(string path, CancellationToken ct)
    {
        using var resp = await http.GetAsync(path, ct);
        if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest) return null;
        resp.EnsureSuccessStatusCode();
        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    private static string? Str(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    public async Task<IsbnInfo?> GetIsbnAsync(string isbn, CancellationToken ct = default)
    {
        var r = await GetOrNull($"isbn/v1/{isbn}", ct);
        if (r is not { } e) return null;
        string[]? authors = e.TryGetProperty("authors", out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToArray()
            : null;
        return new IsbnInfo(isbn, Str(e, "title"), authors, Str(e, "publisher"), Int(e, "year"), Str(e, "synopsis"));
    }

    public async Task<NcmInfo?> GetNcmAsync(string code, CancellationToken ct = default)
    {
        var r = await GetOrNull($"ncm/v1/{code}", ct);
        return r is { } e ? new NcmInfo(Str(e, "codigo") ?? code, Str(e, "descricao")) : null;
    }

    public async Task<RegistroBrInfo?> GetRegistroBrAsync(string domain, CancellationToken ct = default)
    {
        var r = await GetOrNull($"registrobr/v1/{Uri.EscapeDataString(domain)}", ct);
        return r is { } e ? new RegistroBrInfo(Str(e, "fqdn") ?? domain, Str(e, "status") ?? "?", Str(e, "expires-at")) : null;
    }

    public async Task<CepInfo?> GetCepAsync(string cep, CancellationToken ct = default)
    {
        var r = await GetOrNull($"cep/v2/{cep}", ct);
        if (r is not { } e) return null;
        double? lat = null, lng = null;
        if (e.TryGetProperty("location", out var loc) &&
            loc.TryGetProperty("coordinates", out var co))
        {
            if (co.TryGetProperty("latitude", out var la) && double.TryParse(la.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dl)) lat = dl;
            if (co.TryGetProperty("longitude", out var lo) && double.TryParse(lo.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dn)) lng = dn;
        }
        return new CepInfo(Str(e, "cep") ?? cep, Str(e, "street"), Str(e, "neighborhood"),
            Str(e, "city"), Str(e, "state"), lat, lng, "brasilapi");
    }

    public async Task<IReadOnlyList<PixParticipant>> GetPixParticipantsAsync(CancellationToken ct = default)
    {
        var r = await GetOrNull("pix/v1/participants", ct);
        if (r is not { } e || e.ValueKind != JsonValueKind.Array) return [];
        return e.EnumerateArray().Select(p => new PixParticipant(
            Str(p, "ispb") ?? "",
            Str(p, "nome") ?? "",
            Str(p, "nome_reduzido") ?? "",
            Str(p, "tipo_participacao") ?? "")).ToList();
    }
}
