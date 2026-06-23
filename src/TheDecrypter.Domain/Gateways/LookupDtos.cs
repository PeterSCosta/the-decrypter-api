namespace TheDecrypter.Domain.Gateways;

public record IsbnInfo(string Isbn, string? Title, string[]? Authors, string? Publisher, int? Year, string? Synopsis);

public record NcmInfo(string Codigo, string? Descricao);

public record RegistroBrInfo(string Fqdn, string Status, string? ExpiresAt);

public record ProductInfo(string Barcode, string? Name, string? Brands, string? Quantity);

public record PixParticipant(string Ispb, string Nome, string NomeReduzido, string Tipo);

public record CepInfo(
    string Cep, string? Logradouro, string? Bairro, string? Localidade,
    string? Uf, double? Lat, double? Lng, string Source);

public record W3wInfo(string Words, double Lat, double Lng, string? NearestPlace, string? Country);

public record GeocodeInfo(string Query, double Lat, double Lng, string? DisplayName);
