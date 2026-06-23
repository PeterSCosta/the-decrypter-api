namespace TheDecrypter.Domain.Gateways;

/// <summary>Produto pelo código de barras (Open Food Facts). null se não achar.</summary>
public interface IProductGateway
{
    Task<ProductInfo?> GetProductAsync(string barcode, CancellationToken ct = default);
}
