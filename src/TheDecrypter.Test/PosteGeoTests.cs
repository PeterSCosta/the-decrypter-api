using System.Text.RegularExpressions;
using TheDecrypter.Domain.Search;
using Xunit;

namespace TheDecrypter.Test;

public class PosteGeoTests
{
    /// <summary>
    /// A guarda que importa: a escala do C# tem de ser **idêntica** à da coluna
    /// gerada no `schema.sql`. Se divergirem, o Postgres para de usar o índice
    /// GiST e não avisa — a consulta continua devolvendo o resultado certo, só
    /// que varrendo a tabela inteira. Nenhum teste de resultado pegaria isso.
    /// </summary>
    [Fact]
    public void a_escala_bate_com_a_coluna_gerada_do_schema()
    {
        var raiz = AppContext.BaseDirectory;
        // bin/Debug/net10.0 → sobe até a raiz do repo
        var schema = Path.GetFullPath(Path.Combine(raiz, "../../../../../db/schema.sql"));
        Assert.True(File.Exists(schema), $"schema.sql não encontrado em {schema}");

        var ddl = File.ReadAllText(schema);
        var m = Regex.Match(ddl, @"GENERATED ALWAYS AS \(point\(lat,\s*lng\s*\*\s*([0-9.]+)\)\)");
        Assert.True(m.Success, "expressão de `coord_bnu` não encontrada no schema.sql");

        Assert.Equal(PosteGeo.EscalaLng, double.Parse(m.Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void distancia_em_metros_bate_com_um_par_conhecido()
    {
        // Dois postes vizinhos na Rua XV de Novembro, ~13 m um do outro.
        var (latA, lngA) = (-26.919607, -49.065407);
        var (latB, lngB) = (-26.919505, -49.065343);
        var d = Math.Sqrt(
            Math.Pow(latA - latB, 2) +
            Math.Pow(PosteGeo.EscalarLng(lngA) - PosteGeo.EscalarLng(lngB), 2))
            * PosteGeo.MetrosPorGrau;
        Assert.InRange(d, 10, 16);
    }
}
