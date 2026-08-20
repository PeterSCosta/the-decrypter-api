using TheDecrypter.Domain.Search;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// O portão de forma do ID da IMDb, no servidor.
///
/// Ele é a única porta desta casa que sai para fora da nossa infraestrutura, e
/// por isso é a que mais precisa ser estreita: cada `tt` reconhecido é uma
/// consulta SPARQL no Wikidata.
/// </summary>
public class LookupShapeFilmeTests
{
    [Theory]
    [InlineData("tt0111161")]   // 7 dígitos
    [InlineData("tt15398776")]  // 8 dígitos
    [InlineData("TT0111161")]   // caixa não importa
    [InlineData("  tt0111161 ")]
    public void Id_de_filme_abre_a_consulta_de_filme(string entrada)
    {
        Assert.Equal(Consultas.Filme, LookupShape.De(entrada));
    }

    /// <summary>
    /// O ID de filme abre a consulta de filme **e mais nada**. Se ele passasse
    /// adiante, "tt0111161" viraria também busca de rua (tem 2 letras e 7
    /// dígitos) e a bancada faria consulta de banco a cada tecla de um texto
    /// que já foi reconhecido.
    /// </summary>
    [Fact]
    public void E_so_a_de_filme()
    {
        var q = LookupShape.De("tt0111161");
        Assert.False(q.HasFlag(Consultas.RuaOuBairro));
        Assert.False(q.HasFlag(Consultas.Municipio));
        Assert.False(q.HasFlag(Consultas.Plaqueta));
    }

    [Theory]
    [InlineData("tt111")]       // curto
    [InlineData("tt123456789")] // longo
    [InlineData("nm0000151")]   // é PESSOA na IMDb, não obra — outra porta
    [InlineData("t0111161")]
    [InlineData("0111161")]
    [InlineData("tt01111a1")]
    public void O_que_nao_e_id_de_filme_nao_abre_a_porta(string entrada)
    {
        Assert.False(LookupShape.De(entrada).HasFlag(Consultas.Filme));
    }

    /// <summary>
    /// A porta nova não pode ter fechado nenhuma das antigas — é o risco de
    /// somar um `return` antecipado num portão que era todo acumulativo.
    /// </summary>
    [Fact]
    public void As_portas_que_ja_existiam_continuam_abrindo()
    {
        Assert.True(LookupShape.De("89010000").HasFlag(Consultas.CepExato));
        Assert.True(LookupShape.De("4202404").HasFlag(Consultas.Municipio));
        Assert.True(LookupShape.De("GRU").HasFlag(Consultas.Aeroporto));
        Assert.True(LookupShape.De("Rua Sao Paulo").HasFlag(Consultas.RuaOuBairro));
        Assert.Equal(Consultas.Nenhuma, LookupShape.De(""));
    }

    /// <summary>
    /// O achado que este teste travava — `Rua XV` caindo em <c>CepCuringa</c>
    /// porque o `X` acionava o ramo de curinga de CEP — está CONSERTADO: o
    /// portão agora pede a forma inteira do padrão, não só a presença do
    /// curinga. Em Blumenau essa é a rua principal.
    ///
    /// A cobertura larga do portão de curinga mora em
    /// <see cref="LookupShapeCuringaTests"/>; aqui fica o caso que nomeia o
    /// achado, onde ele foi anotado.
    /// </summary>
    [Fact]
    public void Rua_com_X_e_busca_de_rua_e_nao_curinga_de_cep()
    {
        var q = LookupShape.De("Rua XV");
        Assert.True(q.HasFlag(Consultas.RuaOuBairro));
        Assert.False(q.HasFlag(Consultas.CepCuringa));
    }

    [Fact]
    public void A_forma_e_uma_so_nos_dois_lugares_que_precisam_dela()
    {
        // `ImdbId` existe justamente para o portão e o gateway não terem cada
        // um a sua cópia da regra.
        Assert.True(ImdbId.Parece("tt0111161"));
        Assert.Equal("tt0111161", ImdbId.Normalizar("  TT0111161 "));
        Assert.Equal(string.Empty, ImdbId.Normalizar("tt111"));
    }
}
