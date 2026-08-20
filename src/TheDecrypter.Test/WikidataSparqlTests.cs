using TheDecrypter.Domain.Gateways;
using TheDecrypter.Domain.Search;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// AS OUTRAS DUAS ESPÉCIES — propriedade (`P…`) e lexema (`L…`).
///
/// Nem tudo no Wikidata começa com `Q`, e a diferença importa: `Q2` é a Terra,
/// `P345` é o CAMPO "identificador IMDb" e `L1` é a PALAVRA "ama". Todas são
/// acerto exato — um código aponta para um registro e só um —, que é o oposto
/// do problema de ambiguidade que manteve a busca por nome fechada.
/// </summary>
public class WikidataSparqlTests
{
    private static string Fix(string nome) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"wd-{nome}.json"));

    // ── a forma ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Q2", EspecieWikidata.Item)]
    [InlineData("P345", EspecieWikidata.Propriedade)]
    [InlineData("L1", EspecieWikidata.Lexema)]
    [InlineData("q155", EspecieWikidata.Item)]
    [InlineData("p625", EspecieWikidata.Propriedade)]
    public void A_especie_sai_do_prefixo(string codigo, EspecieWikidata esperada) =>
        Assert.Equal(esperada, CodigoWikidata.Especie(codigo));

    /// <summary>
    /// `Q0` e `P01` não existem, e aceitá-los faria a bancada gastar requisição
    /// com forma que nunca resolve.
    /// </summary>
    [Theory]
    [InlineData("Q0")]
    [InlineData("P01")]
    [InlineData("L0")]
    [InlineData("X155")]
    [InlineData("155")]
    [InlineData("Q")]
    [InlineData("Q12x")]
    [InlineData("")]
    public void O_que_nao_e_codigo_nao_tem_especie(string lixo) =>
        Assert.Null(CodigoWikidata.Especie(lixo));

    [Fact]
    public void A_consulta_muda_com_a_especie()
    {
        Assert.Contains("rdfs:label", WikidataSparql.Consulta("P345"));
        Assert.Contains("wikibase:lemma", WikidataSparql.Consulta("L1"));
        // `Q…` NÃO vem por aqui: ele tem consulta própria, que também precisa
        // responder "é filme?".
        Assert.Equal(string.Empty, WikidataSparql.Consulta("Q2"));
        Assert.Equal(string.Empty, WikidataSparql.Consulta("tt0111161"));
    }

    // ── propriedade ──────────────────────────────────────────────────────

    /// <summary>
    /// `P345` é o campo, e a descrição dele explica justamente os prefixos que
    /// a bancada usa em outro lugar — `tt` de título, `nm` de pessoa.
    /// </summary>
    [Fact]
    public void Uma_propriedade_responde_o_que_o_campo_significa()
    {
        var i = WikidataSparql.Ler("P345", Fix("prop-p345"))!;
        Assert.Equal("identificador IMDb", i.Rotulo);
        Assert.Contains("tt", i.Descricao!);
        Assert.Contains("propriedade do Wikidata", i.Tipos!);
        Assert.False(i.EhFilme);
    }

    /// <summary>
    /// Dizer que é PROPRIEDADE evita que alguém leia "identificador IMDb" como
    /// se fosse uma coisa chamada assim.
    /// </summary>
    [Fact]
    public void A_propriedade_se_anuncia_como_campo_e_nao_como_coisa()
    {
        var i = WikidataSparql.Ler("P625", Fix("prop-p625"))!;
        Assert.Contains("coordenadas", i.Rotulo!);
        Assert.Contains("propriedade do Wikidata", i.Tipos!);
        // Ela FALA de coordenada e não TEM coordenada — a diferença é o item.
        Assert.Null(i.Lat);
        Assert.Null(i.Lng);
    }

    // ── lexema ───────────────────────────────────────────────────────────

    /// <summary>
    /// A LÍNGUA É O ITEM. `L1` é "ama", mas em SUMÉRIO — ler isso como
    /// português seria a resposta errada com melhor disfarce que este card
    /// poderia dar numa bancada cujo vocabulário é pt-BR.
    /// </summary>
    [Fact]
    public void Um_lexema_traz_lema_classe_e_a_lingua()
    {
        var i = WikidataSparql.Ler("L1", Fix("lex-l1"))!;
        Assert.Equal("ama", i.Rotulo);
        Assert.Contains("lexema", i.Tipos!);
        Assert.Contains(i.Tipos!, t => t.Contains("sumér", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("mãe", i.Descricao!);
    }

    [Fact]
    public void Outro_lexema_em_outra_lingua()
    {
        var i = WikidataSparql.Ler("L2", Fix("lex-l2"))!;
        Assert.Equal("first", i.Rotulo);
        Assert.Contains(i.Tipos!, t => t.Contains("inglês", StringComparison.OrdinalIgnoreCase));
    }

    // ── o silêncio ───────────────────────────────────────────────────────

    /// <summary>
    /// Código que o Wikidata não conhece devolve nulo. Uma casca com o número
    /// dentro seria afirmar existência sem evidência — e na tela ela apareceria
    /// com a mesma cara de um acerto.
    /// </summary>
    [Fact]
    public void Codigo_inexistente_devolve_nulo()
    {
        Assert.Null(WikidataSparql.Ler("P99999999", Fix("prop-inexistente")));
        Assert.Null(WikidataSparql.Ler("L99999999", Fix("lex-inexistente")));
    }

    [Fact]
    public void Corpo_invalido_nao_estoura()
    {
        foreach (var lixo in new[] { "", "{}", "não é json" })
        {
            Assert.Null(WikidataSparql.Ler("P345", lixo));
            Assert.Null(WikidataSparql.Ler("L1", lixo));
        }
    }

    [Fact]
    public void O_portao_de_forma_abre_para_as_quatro_chaves()
    {
        foreach (var c in new[] { "tt1074638", "Q4941", "P345", "L1" })
            Assert.True(LookupShape.De(c).HasFlag(Consultas.Filme), c);
        foreach (var c in new[] { "Q0", "155", "X1" })
            Assert.False(LookupShape.De(c).HasFlag(Consultas.Filme), c);
    }
}
