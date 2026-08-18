namespace TheDecrypter.Domain.Search;

/// <summary>
/// A inscrição imobiliária de Blumenau, nas grafias que aparecem na vida real.
///
/// ── O NÚMERO ────────────────────────────────────────────────────────────────
/// São 15 dígitos em seis grupos, `T R DD QQQQ LLLL UUU`:
///   distrito · setor · subsetor · quadra(4) · lote(4) · unidade(3)
/// A unidade é `000` no lote inteiro; apartamento e box têm a própria.
///
/// ── AS TRÊS GRAFIAS, E O QUE MUDA ENTRE ELAS ────────────────────────────────
/// O carnê do IPTU escreve com pontuação e SEM os zeros à esquerda
/// (`4.1.24.20.2`); o geoportal guarda os 15 dígitos (`412400200002000`) e
/// também uma forma com hífen e sem zeros (`4-1-24-20-2`, o campo `IQ`).
///
/// **A pegadinha medida:** consultar `4.1.24.18.26.2` não acha nada — os zeros
/// à esquerda são obrigatórios na chave. Quem normaliza aqui é esta classe, e
/// é por isso que ela existe: sem ela, cada chamador reinventaria o padding e
/// um deles esqueceria.
///
/// ── E A QUARTA GRAFIA: O IQ SEM OS HÍFENS ───────────────────────────────────
/// A tela do geoportal mostra `4-1-24-16-28` bem grande, e quem copia à mão
/// digita `41241628`. Esse número **não se lê por regra**: sem os hífens não se
/// sabe onde a quadra termina e o lote começa — `41101634` tanto pode ser
/// `4-1-10-16-34` quanto `4-1-10-1-634`, e as duas EXISTEM.
///
/// Então não se adivinha: enumeram-se todas as fatias que as larguras do
/// formato permitem (2 a 7 delas, nunca mais) e **quem desempata é a base**.
/// Medido nas 84.539 linhas: 82.603 números distintos, dos quais 1.886 (2,3%)
/// admitem mais de um agrupamento REAL — e nenhum admite mais de três. É pouco
/// o bastante para mostrar os candidatos em vez de escolher um por sorteio.
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
    ///
    /// `Candidatos` são os IQ possíveis: um só quando a grafia já separa os
    /// grupos, vários quando vieram dígitos colados. Quem busca usa SEMPRE esta
    /// lista — assim o caminho é um só, e o caso ambíguo não é uma exceção
    /// esquecida em algum chamador.
    /// </summary>
    public record Forma(string? Inscricao, string? Iq, IReadOnlyList<string> Candidatos);

    /// <summary>
    /// Aceita 15 dígitos crus, 12 dígitos crus, 5–10 dígitos crus (o IQ sem os
    /// hífens) ou 5–6 grupos separados por `.` `-` `/` ou espaço.
    /// </summary>
    public static Forma? Normalizar(string? bruto)
    {
        var t = (bruto ?? string.Empty).Trim();
        if (t.Length is < 5 or > 30) return null;

        // Caminho 1: só dígitos.
        if (t.All(char.IsDigit))
        {
            // 12 dígitos = sem a unidade; completa com o lote inteiro.
            if (t.Length == 12) return Montar(t[..1], t[1..2], t[2..4], t[4..8], t[8..12], "000");
            if (t.Length == 15) return Montar(t[..1], t[1..2], t[2..4], t[4..8], t[8..12], t[12..]);

            // O IQ colado. A faixa é 5–10 porque foi o que a base tem (o
            // formato comportaria 12, mas 12 dígitos já querem dizer outra
            // coisa aqui em cima, e 11 é telefone celular).
            if (t.Length is >= 5 and <= 10)
            {
                var fatias = Fatiar(t);
                return fatias.Count == 0 ? null : new Forma(null, null, fatias);
            }

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

    /// <summary>
    /// Todas as leituras que as larguras do formato permitem para uma fila de
    /// dígitos: distrito 1 · setor 1 · subsetor 1–2 · quadra 1–4 · lote 1–4.
    ///
    /// Um grupo com mais de um dígito **não pode começar em zero**, porque o IQ
    /// da base nasce sem os zeros à esquerda — é essa regra que corta metade
    /// das fatias e mantém a lista com um punhado de itens em vez de dezenas.
    /// </summary>
    private static List<string> Fatiar(string d)
    {
        var saida = new List<string>();
        for (var sub = 1; sub <= 2; sub++)
            for (var qua = 1; qua <= 4; qua++)
                for (var lot = 1; lot <= 4; lot++)
                {
                    if (2 + sub + qua + lot != d.Length) continue;
                    var g = new[]
                    {
                        d[..1], d.Substring(1, 1), d.Substring(2, sub),
                        d.Substring(2 + sub, qua), d.Substring(2 + sub + qua, lot),
                    };
                    if (g.Any(p => p.Length > 1 && p[0] == '0')) continue;
                    saida.Add(string.Join('-', g));
                }
        return saida;
    }

    private static Forma? Montar(string t, string r, string d, string q, string l, string? u)
    {
        // O IQ da base é sem zeros à esquerda — e é assim que ele tem de ser
        // reconstruído, senão a busca pela outra coluna não casa.
        var iq = $"{Sem(t)}-{Sem(r)}-{Sem(d)}-{Sem(q)}-{Sem(l)}";

        // Sem a unidade não se inventa `000`: o que existe é o IQ.
        if (u is null) return new Forma(null, iq, [iq]);

        var insc = $"{t.PadLeft(1, '0')}{r.PadLeft(1, '0')}{d.PadLeft(2, '0')}" +
                   $"{q.PadLeft(4, '0')}{l.PadLeft(4, '0')}{u.PadLeft(3, '0')}";
        return insc.Length == 15 ? new Forma(insc, iq, [iq]) : null;
    }

    private static string Sem(string s) => s.TrimStart('0') is { Length: > 0 } t ? t : "0";
}
