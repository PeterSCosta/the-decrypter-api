using TheDecrypter.Domain.Gateways;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// O ITEM QUALQUER — o que sobra quando o `Q…` não é filme.
///
/// A avaliação da Onda 10 recusou resolver NOME → entidade, e com razão:
/// "Bacurau" é filme e é ave, "Maria" são 113 candidatos. Um QID não tem esse
/// problema — ele identifica UM item e só um, por construção. É acerto exato,
/// não triagem, e por isso este caminho existe e aquele não.
/// </summary>
public class ItemWikidataTests
{
    private static string Fix(string nome) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"filme-{nome}.json"));

    [Fact]
    public void Um_planeta_responde_o_que_ele_e()
    {
        var i = FilmeSparql.LerItem("Q2", Fix("qid-terra"))!;
        Assert.Equal("Terra", i.Rotulo);
        Assert.Equal("pt-BR", i.Lingua);
        Assert.Contains("planeta", i.Descricao!);
        Assert.False(i.EhFilme);
    }

    /// <summary>
    /// A COORDENADA É O QUE MAIS VALE AQUI: ela cai direto no domínio central
    /// desta bancada. E a ordem do WKT é LONGITUDE primeiro — trocá-la põe o
    /// Brasil no oceano Índico.
    /// </summary>
    [Fact]
    public void Um_pais_vem_com_ponto_no_mapa_e_na_ordem_certa()
    {
        var i = FilmeSparql.LerItem("Q155", Fix("qid-brasil"))!;
        Assert.Equal("Brasil", i.Rotulo);
        Assert.NotNull(i.Lat);
        Assert.NotNull(i.Lng);
        // Brasil: latitude por volta de −14, longitude por volta de −53.
        Assert.InRange(i.Lat!.Value, -20, -8);
        Assert.InRange(i.Lng!.Value, -60, -45);
    }

    /// <summary>
    /// O rótulo de Douglas Adams vive só em `mul`. Uma escada de língua que
    /// parasse em `en` devolveria zero para um item que existe — falso-negativo
    /// silencioso, que é o defeito que esta casa proíbe por escrito.
    /// </summary>
    [Fact]
    public void O_rotulo_em_mul_e_encontrado_e_a_lingua_viaja_junto()
    {
        var i = FilmeSparql.LerItem("Q42", Fix("qid-pessoa"))!;
        Assert.Equal("Douglas Adams", i.Rotulo);
        Assert.Equal("mul", i.Lingua);
        Assert.False(i.EhFilme);
        // O identificador que ele carrega é de PESSOA, não de título.
        Assert.StartsWith("nm", i.ImdbId);
    }

    [Fact]
    public void Um_filme_e_item_e_filme_ao_mesmo_tempo()
    {
        var i = FilmeSparql.LerItem("Q220741", Fix("qid-cidadededeus"))!;
        Assert.Equal("Cidade de Deus", i.Rotulo);
        Assert.True(i.EhFilme);
        Assert.Equal("tt0317248", i.ImdbId);
        // E a leitura de filme, da MESMA resposta, também responde.
        Assert.NotNull(FilmeSparql.Ler("Q220741", Fix("qid-cidadededeus")));
    }

    /// <summary>
    /// Um QID sem rótulo e sem descrição não é item: é um número que o Wikidata
    /// não conhece. Devolver uma casca com o número dentro seria afirmar
    /// existência sem evidência.
    /// </summary>
    [Fact]
    public void Qid_desconhecido_devolve_nulo()
    {
        Assert.Null(FilmeSparql.LerItem("Q999999999", Fix("qid-inexistente")));
    }

    [Fact]
    public void Pela_porta_do_tt_nao_ha_item()
    {
        // `LerItem` só responde a QID: um `tt…` não tem número de item para
        // devolver, e inventar um seria dizer o que não se sabe.
        Assert.Null(FilmeSparql.LerItem("tt0111161", Fix("shawshank")));
    }

    [Fact]
    public void Corpo_invalido_nao_estoura()
    {
        foreach (var lixo in new[] { "", "{}", "não é json" })
            Assert.Null(FilmeSparql.LerItem("Q2", lixo));
    }
}
