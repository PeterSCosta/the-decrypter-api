namespace TheDecrypter.Domain.Entities;

/// <summary>Município do IBGE (código de 7 dígitos → nome + UF).</summary>
public class Municipio
{
    public int CodigoIbge { get; set; }
    public string Nome { get; set; } = default!;
    public string Uf { get; set; } = default!;
}
