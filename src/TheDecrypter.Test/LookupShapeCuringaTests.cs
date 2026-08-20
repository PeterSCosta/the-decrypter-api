using TheDecrypter.Domain.Search;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// O portão do curinga de CEP.
///
/// Ele é o único ramo do <see cref="LookupShape"/> que sai com <c>return</c>
/// sem ser uma forma fechada por natureza (como o `tt` da IMDb), e por isso o
/// único onde um portão largo demais não deixa passar ruído — ele APAGA as
/// outras consultas. Foi o que aconteceu: bastava um `x` no texto, e toda rua
/// com X perdia a busca de rua e de poste.
/// </summary>
public class LookupShapeCuringaTests
{
    /// <summary>Rua com X é rua. Todas de Blumenau, todas medidas no achado.</summary>
    [Theory]
    [InlineData("Rua XV")]
    [InlineData("XV de Novembro")]
    [InlineData("Rua XV de Novembro")]
    [InlineData("Rua Max Tavares")]
    [InlineData("Alexandre")]
    [InlineData("Rua Felix")]
    public void Nome_de_rua_com_x_abre_rua_e_nao_curinga(string entrada)
    {
        var q = LookupShape.De(entrada);
        Assert.True(q.HasFlag(Consultas.RuaOuBairro), $"{entrada} devia abrir busca de rua");
        Assert.False(q.HasFlag(Consultas.CepCuringa), $"{entrada} não é padrão de CEP");
    }

    /// <summary>
    /// E o padrão de verdade continua entrando — com os separadores que o
    /// `CepPattern` ignora.
    /// </summary>
    [Theory]
    [InlineData("88xxx500")]
    [InlineData("88XXX500")]
    [InlineData("880105x0")]
    [InlineData("x8300000")]
    [InlineData("88*10500")]
    [InlineData("88?10500")]
    [InlineData("88_10500")]
    [InlineData("88010-5x0")]
    [InlineData("  88xxx500 ")]
    public void Padrao_de_cep_com_curinga_abre_o_curinga(string entrada)
    {
        Assert.Equal(Consultas.CepCuringa, LookupShape.De(entrada));
    }

    /// <summary>
    /// O `return` do ramo é de propósito, e este é o motivo: `x` é letra para o
    /// <c>char.IsLetter</c>, então "88xxx500" tem três "letras" e cairia
    /// também em <see cref="Consultas.RuaOuBairro"/> se o ramo acumulasse.
    /// </summary>
    [Fact]
    public void Mascara_de_cep_nao_vira_busca_de_rua()
    {
        var q = LookupShape.De("88xxx500");
        Assert.False(q.HasFlag(Consultas.RuaOuBairro));
        Assert.False(q.HasFlag(Consultas.CidNome));
    }

    /// <summary>
    /// Um dígito e sete curingas É consulta — "todos os CEPs que começam com
    /// 8". A máscara de oito posições é ancorada e o prefixo até o primeiro
    /// curinga vira o `LIKE '8%'` do índice, então esta é das perguntas mais
    /// baratas daqui, não das mais caras. Um piso de dígitos no portão barrava
    /// exatamente ela.
    /// </summary>
    [Theory]
    [InlineData("8xxxxxxx")]
    [InlineData("88xxxxxx")]
    [InlineData("880xxxxx")]
    [InlineData("xxxxx500")]
    [InlineData("8x")]
    [InlineData("88xx")]
    [InlineData("8801x")]
    public void Curinga_com_poucos_digitos_abre_a_consulta(string entrada)
    {
        Assert.Equal(Consultas.CepCuringa, LookupShape.De(entrada));
    }

    /// <summary>
    /// A máscara larga chega ANCORADA no repositório, com o prefixo que o
    /// índice atende — é o que faz `8xxxxxxx` ser barato apesar de casar com
    /// muito. Se ela chegasse como substring, o `LIKE '%'` varreria a tabela.
    /// </summary>
    [Fact]
    public void Mascara_larga_chega_ancorada_e_com_prefixo_de_indice()
    {
        var p = CepPattern.Traduzir("8xxxxxxx")!;
        Assert.True(p.Ancorado);
        Assert.Equal(@"^8\d\d\d\d\d\d\d$", p.Regex);
        Assert.Equal("8%", p.Like);
    }

    /// <summary>
    /// O único piso que sobra: sem nenhum dígito o padrão não filtra nada —
    /// `xxxxxxxx` casa com todo CEP que existe. Isso não é consulta, é o acervo.
    /// </summary>
    [Theory]
    [InlineData("x")]
    [InlineData("xxx")]
    [InlineData("xxxxxxxx")]
    [InlineData("?*_")]
    public void Curinga_sem_digito_nenhum_nao_abre_a_consulta(string entrada)
    {
        Assert.False(LookupShape.De(entrada).HasFlag(Consultas.CepCuringa));
    }

    /// <summary>Nove posições não são um CEP, por mais dígito que tenham.</summary>
    [Fact]
    public void Padrao_longo_demais_nao_abre_a_consulta()
    {
        Assert.False(LookupShape.De("123456789x").HasFlag(Consultas.CepCuringa));
    }

    /// <summary>
    /// O portão virou público para o `/cep/export` chamar O MESMO, e não um
    /// parecido: `xxxxxxxx` traduz para um regex válido (`^\d{8}$`) e, sem esta
    /// regra, uma requisição só levaria a base de CEP inteira num arquivo.
    /// </summary>
    [Theory]
    [InlineData("8xxxxxxx", true)]
    [InlineData("88xxx500", true)]
    [InlineData("88010-5x0", true)]
    [InlineData("  88xxx500  ", true)]
    [InlineData("xxxxxxxx", false)]
    [InlineData("????????", false)]
    [InlineData("Rua XV", false)]
    [InlineData("88010500", false)]
    public void O_portao_publico_e_o_mesmo_que_o_export_usa(string entrada, bool passa)
    {
        Assert.Equal(passa, LookupShape.ParecePadraoDeCep(entrada));
    }

    /// <summary>
    /// O curinga não pode ter roubado as portas vizinhas — é o mesmo risco que
    /// o ramo do filme correu, e o mesmo cheque.
    /// </summary>
    [Fact]
    public void As_portas_vizinhas_continuam_abrindo()
    {
        Assert.True(LookupShape.De("88010500").HasFlag(Consultas.CepExato));
        Assert.True(LookupShape.De("Rua Sao Paulo").HasFlag(Consultas.RuaOuBairro));
        Assert.True(LookupShape.De("99861").HasFlag(Consultas.Plaqueta));
        Assert.True(LookupShape.De("GRU").HasFlag(Consultas.Aeroporto));
        Assert.True(LookupShape.De("tt0111161").HasFlag(Consultas.Filme));
    }
}
