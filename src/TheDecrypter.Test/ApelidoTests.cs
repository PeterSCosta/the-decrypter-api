using TheDecrypter.Domain.Auth;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// A regra do apelido — que é também a regra que roteia o login entre as duas
/// colunas. Um furo aqui não é validação frouxa: é conta de outra pessoa.
/// </summary>
public class ApelidoTests
{
    [Theory]
    [InlineData("peter")]
    [InlineData("ana.silva")]
    [InlineData("jo-ao")]
    [InlineData("x_1")]
    [InlineData("123")]
    [InlineData("abcdefghijklmnopqrstuvwx")] // 24, o teto
    public void Aceita_o_que_uma_pessoa_escolheria(string bom) =>
        Assert.True(Apelido.Valido(Apelido.Normalizar(bom)!));

    [Theory]
    [InlineData("ab")] // curto demais
    [InlineData("abcdefghijklmnopqrstuvwxy")] // 25, um a mais que o teto
    [InlineData(".peter")] // não começa com letra ou dígito
    [InlineData("-peter")]
    [InlineData("_peter")]
    [InlineData("peter silva")] // espaço no meio
    [InlineData("josé")] // acento
    [InlineData("peter!")]
    public void Recusa_o_que_nao_e_apelido(string ruim) =>
        Assert.False(Apelido.Valido(Apelido.Normalizar(ruim)!));

    [Fact]
    public void O_padrao_e_ancorado_e_isso_e_o_que_mata_o_homoglifo()
    {
        // ESTE é o caso que uma regex sem `^…$` deixaria passar: `Regex.IsMatch`
        // procura SUBSTRING em .NET, então "pеter" com um `е` cirílico no meio
        // casaria pelo "p" sozinho — e viraria um apelido visualmente idêntico
        // ao de outra pessoa.
        Assert.False(Apelido.Valido(Apelido.Normalizar("pеter")!));
        Assert.False(Apelido.Valido(Apelido.Normalizar("аdmin")!)); // `а` cirílico
        Assert.False(Apelido.Valido(Apelido.Normalizar("ｐeter")!)); // largura total
    }

    [Fact]
    public void O_arroba_e_proibido_e_e_isso_que_sustenta_o_campo_unico()
    {
        // Se um apelido pudesse conter `@`, o roteador do login mandaria a busca
        // para a coluna errada — e um apelido "peter@x.com" ficaria inalcançável
        // ou, pior, disputaria a conta de quem tem esse e-mail.
        Assert.False(Apelido.Valido(Apelido.Normalizar("peter@x.com")!));
        Assert.True(Apelido.PareceEmail("peter@x.com"));
        Assert.False(Apelido.PareceEmail("peter"));
    }

    [Fact]
    public void Normaliza_caixa_e_espaco_das_pontas_e_branco_vira_nulo()
    {
        Assert.Equal("peter", Apelido.Normalizar("  PeTer  "));
        // Nulo, e não string vazia: duas contas gravadas com '' colidiriam no
        // índice único de um campo que é opcional.
        Assert.Null(Apelido.Normalizar("   "));
        Assert.Null(Apelido.Normalizar(null));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Administrador")]
    [InlineData("root")]
    [InlineData("suporte")]
    public void Reservados_nao_sao_privilegio_mas_confundem_quem_aprova(string r) =>
        Assert.True(Apelido.EhReservado(Apelido.Normalizar(r)!));
}
