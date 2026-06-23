using System.Text.Json;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Http.Gateways;

/// <summary>Produto pelo código de barras (Open Food Facts). Só alimentos.</summary>
public class OpenFoodFactsGateway(HttpClient http) : IProductGateway
{
    public async Task<ProductInfo?> GetProductAsync(string barcode, CancellationToken ct = default)
    {
        using var resp = await http.GetAsync(
            $"api/v2/product/{barcode}.json?fields=product_name,brands,quantity", ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        var root = doc.RootElement;
        if (!root.TryGetProperty("status", out var st) || st.GetInt32() != 1) return null;
        if (!root.TryGetProperty("product", out var p)) return null;

        string? S(string prop) =>
            p.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String && v.GetString()!.Length > 0
                ? v.GetString() : null;

        return new ProductInfo(barcode, S("product_name"), S("brands"), S("quantity"));
    }
}
