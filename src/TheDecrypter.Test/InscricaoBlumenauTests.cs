using TheDecrypter.Domain.Search;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// A inscrição imobiliária de Blumenau — e a pegadinha que fez a consulta
/// falhar quando foi testada na mão: os zeros à esquerda são obrigatórios na
/// chave, mas o carnê do IPTU os omite.
/// </summary>
public class InscricaoBlumenauTests
{
    [Fact]
    public void Quinze_digitos_crus_passam_direto()
    {
        var f = InscricaoBlumenau.Normalizar("412400200002000");
        Assert.Equal("412400200002000", f!.Inscricao);
        Assert.Equal("4-1-24-20-2", f.Iq);
    }

    [Fact]
    public void O_carne_vem_sem_zeros_e_precisa_ganhar_zeros()
    {
        // ESTE é o caso que falha sem normalização: `4.1.24.20.2.0` tem os
        // mesmos números do registro real, mas sem o padding não casa com a PK.
        var f = InscricaoBlumenau.Normalizar("4.1.24.20.2.0");
        Assert.Equal("412400200002000", f!.Inscricao);
    }

    [Fact]
    public void Hifen_barra_e_espaco_valem_como_separador()
    {
        // O que aparece em anotação de gente, em tela de prefeitura e em carnê.
        foreach (var grafia in new[] { "4-1-24-20-2-0", "4/1/24/20/2/0", "4 1 24 20 2 0" })
            Assert.Equal("412400200002000", InscricaoBlumenau.Normalizar(grafia)!.Inscricao);
    }

    [Fact]
    public void Sem_a_unidade_nao_se_inventa_zero_zero_zero()
    {
        // Cinco grupos não dizem qual unidade é. Chutar `000` acharia o lote
        // inteiro quando a pessoa quis o apartamento — então o que sobra é o IQ,
        // e a busca vai pela outra coluna.
        var f = InscricaoBlumenau.Normalizar("4.1.24.20.2");
        Assert.Null(f!.Inscricao);
        Assert.Equal("4-1-24-20-2", f.Iq);
    }

    [Fact]
    public void Doze_digitos_crus_completam_o_lote_inteiro()
    {
        // Aqui o `000` é seguro: 12 dígitos crus são a inscrição do TERRENO, e
        // o formato não comporta unidade.
        Assert.Equal("412400200002000", InscricaoBlumenau.Normalizar("412400200002")!.Inscricao);
    }

    [Theory]
    [InlineData("")]
    [InlineData("41240020000")]      // 11 dígitos: não é forma nenhuma
    [InlineData("4124002000020000")] // 16
    [InlineData("4.1.24.20")]        // 4 grupos
    [InlineData("44.1.24.20.2.0")]   // distrito com 2 dígitos
    [InlineData("4.1.240.20.2.0")]   // subsetor com 3
    [InlineData("a.1.24.20.2.0")]
    public void Recusa_o_que_nao_e_inscricao(string ruim) =>
        Assert.Null(InscricaoBlumenau.Normalizar(ruim));

    [Fact]
    public void O_iq_nasce_sem_zeros_porque_e_assim_que_a_base_guarda()
    {
        // Se o IQ fosse reconstruído COM zeros, a busca pela outra coluna nunca
        // casaria — e o erro seria silencioso, que é o pior tipo.
        var f = InscricaoBlumenau.Normalizar("401240000200002000");
        Assert.Null(f); // 18 dígitos não é forma
        var ok = InscricaoBlumenau.Normalizar("412400200002000");
        Assert.DoesNotContain("-0", ok!.Iq!);
    }

    [Fact]
    public void O_iq_colado_vira_todas_as_fatias_possiveis()
    {
        // O caso que veio da tela do geoportal: lá está escrito
        // `4-1-24-16-28`, e quem copia à mão digita isto.
        var f = InscricaoBlumenau.Normalizar("41241628");
        Assert.Null(f!.Inscricao);
        Assert.Null(f.Iq);
        Assert.Contains("4-1-24-16-28", f.Candidatos);
    }

    [Fact]
    public void A_fatia_ambigua_devolve_as_duas_leituras_reais()
    {
        // MEDIDO na base: as duas existem, e é por isso que a escolha não pode
        // ser feita aqui — quem desempata é o cadastro, não o palpite.
        var c = InscricaoBlumenau.Normalizar("41101634")!.Candidatos;
        Assert.Contains("4-1-10-16-34", c);
        Assert.Contains("4-1-10-1-634", c);
    }

    [Fact]
    public void Grupo_com_zero_a_esquerda_nao_e_fatia_valida()
    {
        // O IQ da base nasce sem zeros à esquerda, então `4-1-0-1-628` não
        // existe como grafia — e cortar essas fatias é o que mantém a lista
        // pequena o bastante para um `IN`.
        foreach (var iq in InscricaoBlumenau.Normalizar("41241628")!.Candidatos)
            Assert.DoesNotContain(iq.Split('-'), g => g.Length > 1 && g[0] == '0');
    }

    [Fact]
    public void A_lista_de_fatias_nunca_estoura()
    {
        // O `IN` do repositório vive desta garantia. Se a largura dos grupos
        // mudar sem ninguém olhar, é aqui que aparece.
        for (var n = 5; n <= 10; n++)
        {
            var c = InscricaoBlumenau.Normalizar(new string('1', n))!.Candidatos;
            Assert.InRange(c.Count, 1, 8);
        }
    }

    [Fact]
    public void A_grafia_separada_tem_um_candidato_so()
    {
        // Com os hífens não há o que adivinhar — e o chamador usa a MESMA
        // lista nos dois casos, então o caminho é um só.
        Assert.Equal(["4-1-24-20-2"], InscricaoBlumenau.Normalizar("4.1.24.20.2")!.Candidatos);
        Assert.Equal(["4-1-24-20-2"], InscricaoBlumenau.Normalizar("412400200002000")!.Candidatos);
    }
}
