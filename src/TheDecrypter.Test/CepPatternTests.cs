using TheDecrypter.Domain.Search;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// A tradução do padrão de CEP tem de casar com `src/features/cep/cep-pattern.ts`
/// do app — é a única regra que existe nos dois idiomas. Os casos aqui são a
/// especificação escrita naquele arquivo.
/// </summary>
public class CepPatternTests
{
    [Fact]
    public void oito_caracteres_viram_mascara_ancorada()
    {
        var p = CepPattern.Traduzir("88xxx500")!;
        Assert.True(p.Ancorado);
        Assert.Equal(@"^88\d\d\d500$", p.Regex);
        Assert.Equal("88%", p.Like);
    }

    /// <summary>
    /// O caso que o C# errava: padrão curto é SUBSTRING, não máscara. Ancorado,
    /// "500" virava `^500$` e não achava CEP nenhum.
    /// </summary>
    [Fact]
    public void padrao_curto_e_substring_e_nao_ancora()
    {
        var p = CepPattern.Traduzir("500")!;
        Assert.False(p.Ancorado);
        Assert.Equal("500", p.Regex);
        // Sem prefixo no LIKE: o acerto pode estar no meio do CEP.
        Assert.Equal("%", p.Like);
    }

    [Fact]
    public void curinga_no_meio_de_padrao_curto()
    {
        var p = CepPattern.Traduzir("8x5")!;
        Assert.Equal(@"8\d5", p.Regex);
        Assert.False(p.Ancorado);
    }

    [Fact]
    public void espaco_ponto_e_traco_somem()
    {
        Assert.Equal(@"^88010500$", CepPattern.Traduzir("88010-500")!.Regex);
    }

    [Fact]
    public void todos_os_curingas_valem()
    {
        foreach (var c in new[] { "x", "X", "*", "_", "?" })
            Assert.Equal(@"88\d", CepPattern.Traduzir($"88{c}")!.Regex);
    }

    [Fact]
    public void mais_de_oito_ou_vazio_nao_traduz()
    {
        Assert.Null(CepPattern.Traduzir("123456789"));
        Assert.Null(CepPattern.Traduzir(""));
        Assert.Null(CepPattern.Traduzir("abc"));
    }
}
