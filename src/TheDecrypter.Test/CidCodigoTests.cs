using TheDecrypter.Domain.Search;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// A forma de um código da CID-10 — que é o que decide se a entrada da bancada
/// vira consulta ou não.
/// </summary>
public class CidCodigoTests
{
    [Theory]
    [InlineData("A00", "A00")]
    [InlineData("a00", "A00")]
    [InlineData("A000", "A000")]
    [InlineData("A00.0", "A000")]
    [InlineData("a00.0", "A000")]
    [InlineData(" F41.1 ", "F411")]
    // O que sobra de um PDF copiado: o separador vira espaço ou hífen.
    [InlineData("I10", "I10")]
    [InlineData("J45 0", "J450")]
    [InlineData("M31-3", "M313")]
    public void Normaliza_as_duas_grafias(string entrada, string esperado) =>
        Assert.Equal(esperado, CidCodigo.Normalizar(entrada));

    [Theory]
    [InlineData("")]
    [InlineData("A0")]           // curto demais
    [InlineData("A0000")]        // a CID-10 não tem 5 posições
    [InlineData("AB00")]         // duas letras
    [InlineData("1000")]         // sem letra
    [InlineData("A.000")]        // separador fora do lugar
    [InlineData("A0.0.0")]       // dois separadores
    [InlineData("A00.A")]        // subcategoria não é letra
    [InlineData("GRU")]          // aeroporto: três letras, nenhum dígito
    [InlineData("88010500")]     // CEP
    public void Recusa_o_que_nao_e_codigo(string entrada) =>
        Assert.Null(CidCodigo.Normalizar(entrada));

    [Fact]
    public void Exibe_com_ponto_so_quando_ha_subcategoria()
    {
        Assert.Equal("A00.0", CidCodigo.Exibir("A000"));
        Assert.Equal("A00", CidCodigo.Exibir("A00"));
    }

    [Fact]
    public void Padrao_de_nome_ancora_em_inicio_de_palavra()
    {
        // Sem a âncora, "cola" acharia "Cólera" e a bancada devolveria doença
        // para quem digitou qualquer coisa.
        Assert.Equal(@"\mdengue", CidCodigo.PadraoDeNome("dengue"));
    }

    [Fact]
    public void Padrao_de_nome_neutraliza_metacaractere()
    {
        // Um parêntese solto faria o Postgres recusar a expressão inteira, e o
        // erro subiria como 500 em vez de "nada encontrado".
        Assert.Equal(@"\mfebre\ \(alta\)", CidCodigo.PadraoDeNome("febre (alta)"));
        // Letra acentuada NÃO é escapada: `\c` mudaria o sentido do padrão.
        Assert.Equal(@"\mcólera", CidCodigo.PadraoDeNome("cólera"));
    }

    [Theory]
    [InlineData("A00")]
    [InlineData("F41.1")]
    [InlineData("i10")]
    public void Portao_pede_a_consulta_de_codigo(string entrada) =>
        Assert.True(LookupShape.De(entrada).HasFlag(Consultas.CidCodigo));

    [Fact]
    public void Portao_pede_a_consulta_por_nome_so_para_texto_sem_digito()
    {
        Assert.True(LookupShape.De("dengue").HasFlag(Consultas.CidNome));
        // Com dígito no meio já é identificador de outra coisa.
        Assert.False(LookupShape.De("rua 25").HasFlag(Consultas.CidNome));
        // Três letras é aeroporto, não doença.
        Assert.False(LookupShape.De("GRU").HasFlag(Consultas.CidNome));
    }

    [Fact]
    public void Codigo_nao_arrasta_as_outras_consultas()
    {
        // "A00" tem uma letra só: não chega ao mínimo do ramo de nome, e não é
        // aeroporto. Se um dia arrastar, é sinal de que o portão vazou.
        var quais = LookupShape.De("A00");
        Assert.Equal(Consultas.CidCodigo, quais);
    }
}
