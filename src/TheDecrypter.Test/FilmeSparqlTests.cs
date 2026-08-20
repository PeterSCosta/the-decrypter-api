using TheDecrypter.Domain.Gateways;
using TheDecrypter.Domain.Search;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// A leitura da ficha de filme, contra respostas REAIS do Wikidata.
///
/// As fixtures foram baixadas do endpoint em 2026-08-20 e não são mocks: o
/// valor delas é serem exatamente o que o serviço devolve, com as unidades e os
/// campos que ele de fato usa. Cada teste abaixo trava uma armadilha que a
/// primeira versão da consulta caiu de verdade.
/// </summary>
public class FilmeSparqlTests
{
    private static string Fix(string nome) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"filme-{nome}.json"));

    private static FilmeInfo Ler(string nome, string id) =>
        FilmeSparql.Ler(id, Fix(nome))!;

    // ── ARMADILHA 1: a duração vem com unidade ──────────────────────────────

    /// <summary>
    /// `Oppenheimer` tem P2047 = 10809 com unidade Q11574 (SEGUNDO). Ler a
    /// quantidade sem a unidade imprime "10809 min" para um filme de 180 — e
    /// foi o que a primeira versão desta consulta fez.
    /// </summary>
    [Fact]
    public void Duracao_em_segundos_vira_minutos()
    {
        Assert.Equal(180, Ler("oppenheimer", "tt15398776").DuracaoMin);
    }

    [Fact]
    public void Duracao_em_minutos_passa_intacta()
    {
        Assert.Equal(142, Ler("shawshank", "tt0111161").DuracaoMin);
        Assert.Equal(115, Ler("voltaaofuturo", "tt0088763").DuracaoMin);
    }

    // ── ARMADILHA 2: a data de lançamento é uma por país ────────────────────

    /// <summary>
    /// <i>Close-Up</i> é de 1990 e tem lançamentos até 1999; <i>Um Sonho de
    /// Liberdade</i> é de 1994 e estreou no Brasil em 1995. Pegar um P577
    /// qualquer devolvia os anos errados nos dois.
    /// </summary>
    [Fact]
    public void Ano_e_o_do_primeiro_lancamento_nao_um_qualquer()
    {
        Assert.Equal(1990, Ler("closeup", "tt0100234").Ano);
        Assert.Equal(1994, Ler("shawshank", "tt0111161").Ano);
        Assert.Equal(2023, Ler("oppenheimer", "tt15398776").Ano);
    }

    // ── ARMADILHA 3: o título brasileiro é APELIDO, não rótulo ──────────────

    /// <summary>
    /// A mais importante das três. `tt0111161` tem rdfs:label@pt-br = "The
    /// Shawshank Redemption" — o inglês ocupando o campo do português. O título
    /// daqui, "Um Sonho de Liberdade", está em skos:altLabel@pt-br. Uma
    /// consulta que lesse só o rótulo concluiria que o Wikidata não tem o
    /// título brasileiro, quando tem.
    /// </summary>
    [Fact]
    public void Titulo_brasileiro_sai_do_apelido_quando_o_rotulo_traz_o_ingles()
    {
        var f = Ler("shawshank", "tt0111161");
        Assert.Equal("Um Sonho de Liberdade", f.TituloBr);
        Assert.Equal("The Shawshank Redemption", f.TituloOriginal);
    }

    /// <summary>
    /// O título de PORTUGAL viaja em campo próprio e nunca é oferecido como o
    /// brasileiro: "Regresso ao Futuro" no lugar de "De Volta Para o Futuro"
    /// seria um nome plausível, em português, e errado.
    /// </summary>
    [Fact]
    public void Titulo_de_portugal_nao_se_mistura_com_o_do_brasil()
    {
        var f = Ler("voltaaofuturo", "tt0088763");
        Assert.Equal("De Volta Para o Futuro", f.TituloBr);
        Assert.Equal("Regresso ao Futuro", f.TituloPt);
        Assert.NotEqual(f.TituloBr, f.TituloPt);
    }

    /// <summary>
    /// O CASO COMUM, e o que a tela precisa saber dizer: o Wikidata não tem
    /// título brasileiro para este filme. `TituloBr` nulo é resposta, não
    /// falha — e é diferente de preencher com o que sobrou.
    /// </summary>
    [Fact]
    public void Sem_titulo_brasileiro_o_campo_fica_nulo_em_vez_de_herdar_o_ingles()
    {
        var f = Ler("jumanji", "tt7975244");
        Assert.Null(f.TituloBr);
        Assert.Equal("Jumanji: The Next Level", f.TituloOriginal);
    }

    /// <summary>
    /// "Difere do original" é fraco demais sozinho: os apelidos pt de <i>Um
    /// Sonho de Liberdade</i> incluem "Shawshank Redemption", que é o título
    /// original sem o artigo. Aceitá-lo devolveria o inglês com etiqueta de
    /// português — o erro que esta classe inteira existe para não cometer.
    /// </summary>
    [Fact]
    public void Apelido_que_e_so_o_original_sem_artigo_nao_conta_como_traducao()
    {
        var f = Ler("shawshank", "tt0111161");
        Assert.Equal("Os Condenados de Shawshank", f.TituloPt);
        Assert.DoesNotContain("Shawshank Redemption", f.TituloPt!);
    }

    // ── O silêncio, que é o outro lado da mesma disciplina ──────────────────

    /// <summary>
    /// ID que o Wikidata não conhece devolve `null` — e `null` aqui significa
    /// "não achei NO WIKIDATA", nunca "o filme não existe". O Wikidata cobre
    /// uma fração do catálogo da IMDb; medido em 2026-08-20, apenas 6,2% dos
    /// filmes de 2019 com ID da IMDb têm título pt-BR.
    /// </summary>
    [Fact]
    public void Id_desconhecido_devolve_nulo()
    {
        Assert.Null(FilmeSparql.Ler("tt99999999", Fix("inexistente")));
    }

    [Fact]
    public void Corpo_invalido_nao_estoura()
    {
        Assert.Null(FilmeSparql.Ler("tt0111161", "isto não é json"));
        Assert.Null(FilmeSparql.Ler("tt0111161", ""));
        Assert.Null(FilmeSparql.Ler("tt0111161", "{}"));
    }

    // ── A consulta ─────────────────────────────────────────────────────────

    /// <summary>
    /// O ID entra por substituição de texto numa consulta SPARQL. Um ID fora da
    /// forma não pode produzir consulta nenhuma — é aqui que isso é garantido,
    /// e não na confiança de quem chama.
    /// </summary>
    [Fact]
    public void Consulta_so_existe_para_id_de_forma_valida()
    {
        Assert.Contains("\"tt0111161\"", FilmeSparql.Consulta("tt0111161"));
        Assert.Contains("\"tt0111161\"", FilmeSparql.Consulta("  TT0111161  "));
        foreach (var lixo in new[] { "tt111", "tt0111161\" } #", "nao", "", "123456789" })
            Assert.Equal(string.Empty, FilmeSparql.Consulta(lixo));
    }

    [Fact]
    public void Campos_extras_vem_quando_existem()
    {
        var f = Ler("oppenheimer", "tt15398776");
        Assert.Contains("Christopher Nolan", f.Direcao!);
        Assert.NotNull(f.WikidataId);
        Assert.StartsWith("Q", f.WikidataId);
        Assert.Equal("Wikidata", f.Fonte);
    }
}

/// <summary>
/// O DEFEITO QUE UM `tt…` COLADO REVELOU.
///
/// A regra de continência existia para matar "Shawshank Redemption" contra "The
/// Shawshank Redemption" — o original sem o artigo, que passaria por tradução.
/// Só que ela também matava "007 - Operação Skyfall", que CONTÉM "Skyfall" e é
/// um título de verdade. O título existia na fonte e a bancada o jogava fora.
///
/// O que separa os dois é o TAMANHO da diferença: 3 caracteres num caso, 11 no
/// outro. O corte de 4 cobre os artigos das duas línguas e nada além deles.
/// </summary>
public class FilmeSparqlContinenciaTests
{
    private static string Fix(string nome) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"filme-{nome}.json"));

    [Fact]
    public void Titulo_que_contem_o_original_mas_e_muito_maior_NAO_e_variacao()
    {
        var f = FilmeSparql.Ler("tt1074638", Fix("skyfall"))!;
        Assert.Equal("Skyfall", f.TituloOriginal);
        // Antes do conserto isto era null: "007 - Operação Skyfall" contém
        // "Skyfall" e era descartado como se fosse o mesmo nome.
        Assert.Equal("007 - Operação Skyfall", f.TituloPt);
    }

    /// <summary>E o caso que a regra existia para pegar continua pego.</summary>
    [Fact]
    public void O_original_sem_o_artigo_continua_sendo_variacao()
    {
        var f = FilmeSparql.Ler("tt0111161", Fix("shawshank"))!;
        Assert.Equal("Um Sonho de Liberdade", f.TituloBr);
        Assert.Equal("Os Condenados de Shawshank", f.TituloPt);
        Assert.DoesNotContain("Shawshank Redemption", f.TituloPt!);
    }

    [Fact]
    public void Skyfall_traz_ano_duracao_e_direcao()
    {
        var f = FilmeSparql.Ler("tt1074638", Fix("skyfall"))!;
        Assert.Equal(2012, f.Ano);
        Assert.Equal(143, f.DuracaoMin);
        Assert.Contains("Sam Mendes", f.Direcao!);
        // O filme se chama igual aqui e lá: não há título brasileiro distinto,
        // e a bancada diz isso em vez de inventar um.
        Assert.Null(f.TituloBr);
    }
}

/// <summary>
/// A SEGUNDA PORTA: o código do Wikidata.
///
/// `Q4941` é o mesmo filme que `tt1074638`, escrito no catálogo do Wikidata em
/// vez do da IMDb — e é o que se copia de uma página do Wikidata. Sem esta
/// porta, a bancada lia `Q4941` como cauda de Geohash e devolvia cinco pontos
/// em Blumenau, sem nunca dizer que aquilo era um filme.
/// </summary>
public class FilmeSparqlPorQidTests
{
    private static string Fix(string nome) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"filme-{nome}.json"));

    [Fact]
    public void As_duas_portas_chegam_no_mesmo_filme()
    {
        var porTt = FilmeSparql.Ler("tt1074638", Fix("skyfall"))!;
        var porQid = FilmeSparql.Ler("Q4941", Fix("qid-skyfall"))!;
        Assert.Equal(porTt.TituloOriginal, porQid.TituloOriginal);
        Assert.Equal(porTt.Ano, porQid.Ano);
        Assert.Equal(porTt.DuracaoMin, porQid.DuracaoMin);
    }

    /// <summary>
    /// Entrando pelo QID, o ID da IMDb sai da resposta — é ele que a pessoa vai
    /// querer copiar, e é a chave do outro catálogo.
    /// </summary>
    [Fact]
    public void Pelo_qid_o_id_da_imdb_vem_junto()
    {
        Assert.Equal("tt1074638", FilmeSparql.Ler("Q4941", Fix("qid-skyfall"))!.ImdbId);
        Assert.Equal("tt0317248", FilmeSparql.Ler("Q220741", Fix("qid-cidadededeus"))!.ImdbId);
    }

    /// <summary>
    /// UM QID SOZINHO NÃO PROMETE NADA. `Q42` é Douglas Adams, e o identificador
    /// que ele carrega é de PESSOA (`nm0010930`), não de título. Quem promete é
    /// a classificação — sem ela, a bancada cala em vez de apresentar uma pessoa
    /// como se fosse um filme.
    /// </summary>
    [Fact]
    public void Qid_que_nao_e_filme_devolve_nulo()
    {
        Assert.Null(FilmeSparql.Ler("Q42", Fix("qid-pessoa")));
    }

    [Fact]
    public void A_consulta_muda_de_ancora_conforme_a_porta()
    {
        Assert.Contains("wdt:P345 \"tt1074638\"", FilmeSparql.Consulta("tt1074638"));
        Assert.Contains("BIND(wd:Q4941 AS ?obra)", FilmeSparql.Consulta("Q4941"));
        Assert.Contains("BIND(wd:Q4941 AS ?obra)", FilmeSparql.Consulta("  q4941 "));
        foreach (var lixo in new[] { "Q", "Q0", "Q12x", "4941", "" })
            Assert.Equal(string.Empty, FilmeSparql.Consulta(lixo));
    }

    [Fact]
    public void O_portao_de_forma_abre_para_as_duas()
    {
        Assert.True(LookupShape.De("tt1074638").HasFlag(Consultas.Filme));
        Assert.True(LookupShape.De("Q4941").HasFlag(Consultas.Filme));
        Assert.False(LookupShape.De("Q0").HasFlag(Consultas.Filme));
        // `4941` sozinho é número, e continua sendo lido como número.
        Assert.False(LookupShape.De("4941").HasFlag(Consultas.Filme));
    }
}
