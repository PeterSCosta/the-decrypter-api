namespace TheDecrypter.Domain.Search;

/// <summary>Que consultas uma entrada da bancada merece.</summary>
[Flags]
public enum Consultas
{
    Nenhuma = 0,
    CepExato = 1,
    CepPrefixoSc = 2,
    Municipio = 4,
    Plaqueta = 8,
    RuaOuBairro = 16,
    CepCuringa = 32,
    Aeroporto = 64,
}

/// <summary>
/// Os portões de forma, **no servidor**.
///
/// Antes eles moravam no `use-decoder.ts`, decidindo qual dataset baixar. Com a
/// consulta pela API, deixá-los no cliente significaria manter a mesma regra
/// escrita em dois idiomas — e elas divergiriam. Aqui é um lugar só, puro e
/// testável; o cliente mantém apenas um portão grosseiro de custo ("isto não
/// parece nada, nem pergunte").
/// </summary>
public static class LookupShape
{
    /// <summary>Acima disto é texto cifrado colado, não identificador.</summary>
    public const int MaxEntrada = 64;

    public static Consultas De(string? entrada)
    {
        var texto = (entrada ?? string.Empty).Trim();
        if (texto.Length == 0 || texto.Length > MaxEntrada) return Consultas.Nenhuma;

        var c = Consultas.Nenhuma;
        var digitos = new string([.. texto.Where(char.IsDigit)]);
        var soDigitos = digitos.Length == texto.Length;

        // Curinga explícito (88xxx500). Vem antes das outras formas porque um
        // padrão com `x` não é nem número nem nome.
        if (texto.Any(ch => "xX*_?".Contains(ch)) && CepPattern.Traduzir(texto) is not null)
            return Consultas.CepCuringa;

        if (soDigitos)
        {
            // 8 dígitos: CEP. Também é o comprimento de um ISPB do PIX, mas esse
            // vive numa lista que o app já carrega inteira.
            if (digitos.Length == 8) c |= Consultas.CepExato;
            // 6 dígitos: CEP sem o prefixo de SC (88/89) ou IBGE sem o dígito.
            if (digitos.Length == 6) c |= Consultas.CepPrefixoSc | Consultas.Municipio;
            if (digitos.Length == 7) c |= Consultas.Municipio;
            // Plaquetas vão de 1 a 6 dígitos (78% têm 5).
            if (digitos.Length is >= 1 and <= 6) c |= Consultas.Plaqueta;
        }
        else if (texto.Count(char.IsLetter) >= 3)
        {
            c |= Consultas.RuaOuBairro;
            // 3 letras = IATA (GRU), 4 = ICAO (SBGR). Só letras, sem espaço.
            if (texto.Length is 3 or 4 && texto.All(char.IsLetter)) c |= Consultas.Aeroporto;
        }

        return c;
    }
}
