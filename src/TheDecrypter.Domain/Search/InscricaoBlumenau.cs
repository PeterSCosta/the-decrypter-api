namespace TheDecrypter.Domain.Search;

/// <summary>
/// A inscrição imobiliária de Blumenau, nas grafias que aparecem na vida real.
///
/// ── O NÚMERO ────────────────────────────────────────────────────────────────
/// São 15 dígitos em seis grupos, `T R DD QQQQ LLLL UUU`:
///   distrito · setor · subsetor · quadra(4) · lote(4) · unidade(3)
/// A unidade é `000` no lote inteiro; apartamento e box têm a própria.
///
/// ── AS DUAS GRAFIAS, E O QUE MUDA ENTRE ELAS ────────────────────────────────
/// O carnê do IPTU escreve com pontuação e SEM os zeros à esquerda
/// (`4.1.24.20.2`); o geoportal guarda os 15 dígitos (`412400200002000`) e
/// também uma forma com hífen e sem zeros (`4-1-24-20-2`, o campo `IQ`).
///
/// **A pegadinha medida:** consultar `4.1.24.18.26.2` não acha nada — os zeros
/// à esquerda são obrigatórios na chave. Quem normaliza aqui é esta classe, e
/// é por isso que ela existe: sem ela, cada chamador reinventaria o padding e
/// um deles esqueceria.
/// </summary>
public static class InscricaoBlumenau
{
    /// <summary>
    /// O que a normalização conseguiu montar.
    ///
    /// `Inscricao` só vem preenchida quando dá para reconstruir os 15 dígitos.
    /// Com cinco grupos (sem a unidade), o que existe é o `Iq` — e a busca tem
    /// de ir pela outra coluna. Devolver os dois evita que o chamador invente
    /// um `000` que talvez não seja o dele.
    /// </summary>
    public record Forma(string? Inscricao, string Iq);

    /// <summary>Aceita 15 dígitos crus, 12 dígitos crus, ou 5–6 grupos separados por `.` `-` ou `/`.</summary>
    public static Forma? Normalizar(string? bruto)
    {
        var t = (bruto ?? string.Empty).Trim();
        if (t.Length is < 7 or > 30) return null;

        // Caminho 1: só dígitos.
        if (t.All(char.IsDigit))
        {
            // 12 dígitos = sem a unidade; completa com o lote inteiro.
            if (t.Length == 12) return Montar(t[..1], t[1..2], t[2..4], t[4..8], t[8..12], "000");
            if (t.Length == 15) return Montar(t[..1], t[1..2], t[2..4], t[4..8], t[8..12], t[12..]);
            return null;
        }

        // Caminho 2: grupos separados. Ponto, hífen e barra são o que aparece
        // em carnê, em tela de prefeitura e em anotação de gente.
        var g = t.Split(['.', '-', '/', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (g.Length is not (5 or 6)) return null;
        if (!g.All(p => p.Length > 0 && p.All(char.IsDigit))) return null;
        if (g[0].Length > 1 || g[1].Length > 1 || g[2].Length > 2) return null;
        if (g[3].Length > 4 || g[4].Length > 4) return null;
        if (g.Length == 6 && g[5].Length > 3) return null;

        return Montar(g[0], g[1], g[2], g[3], g[4], g.Length == 6 ? g[5] : null);
    }

    private static Forma? Montar(string t, string r, string d, string q, string l, string? u)
    {
        // O IQ da base é sem zeros à esquerda — e é assim que ele tem de ser
        // reconstruído, senão a busca pela outra coluna não casa.
        var iq = $"{Sem(t)}-{Sem(r)}-{Sem(d)}-{Sem(q)}-{Sem(l)}";

        // Sem a unidade não se inventa `000`: o que existe é o IQ.
        if (u is null) return new Forma(null, iq);

        var insc = $"{t.PadLeft(1, '0')}{r.PadLeft(1, '0')}{d.PadLeft(2, '0')}" +
                   $"{q.PadLeft(4, '0')}{l.PadLeft(4, '0')}{u.PadLeft(3, '0')}";
        return insc.Length == 15 ? new Forma(insc, iq) : null;
    }

    private static string Sem(string s) => s.TrimStart('0') is { Length: > 0 } t ? t : "0";
}
