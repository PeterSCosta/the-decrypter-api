using System.Text;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Export;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// O CSV da exportação de CEP.
///
/// Quase nada aqui é sobre "gerar CSV" — é sobre o arquivo ABRIR CERTO no Excel
/// em pt-BR, que é onde ele vai ser aberto. Separador, BOM e vírgula decimal são
/// os três jeitos de o arquivo estar tecnicamente correto e mesmo assim inútil.
/// </summary>
public class CepCsvTests
{
    private static Cep Exemplo() => new()
    {
        Code = "88010500",
        Logradouro = "Rua Felipe Schmidt",
        Bairro = "Centro",
        Localidade = "Florianópolis",
        Uf = "SC",
        Lat = -27.557615,
        Lng = -48.495141,
    };

    private static string Texto(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>
    /// O TESTE QUE MAIS IMPORTA, e o menos óbvio: a linha inteira de uma vez.
    ///
    /// A armadilha é que `Cep.Localidade` guarda o nome do MUNICÍPIO e
    /// `Cep.Bairro` guarda o bairro — o front desfaz isso na leitura. Quem ler
    /// o gerador daqui a três meses vai achar que `Localidade → Município` é um
    /// bug e "consertar" trocando os dois; latitude e longitude são dois
    /// `double?` vizinhos e igualmente fáceis de inverter. Nenhum dos outros
    /// testes deste arquivo perceberia — BOM, CRLF e aspas continuariam certos,
    /// e o arquivo passaria a afirmar que "Florianópolis" é bairro, num CSV
    /// onde não há coluna vizinha para desmentir.
    ///
    /// Afirmar a linha inteira prende ordem, destino e número de colunas de uma
    /// vez só.
    /// </summary>
    [Fact]
    public void a_linha_mapeia_cada_campo_na_coluna_certa()
    {
        Assert.Equal(
            "88010-500;Rua Felipe Schmidt;Centro;Florianópolis;SC;-27,557615;-48,495141",
            CepCsv.Linha(Exemplo()));
    }

    [Fact]
    public void o_cabecalho_nomeia_as_mesmas_sete_colunas_da_linha()
    {
        Assert.Equal(7, CepCsv.Cabecalho.Split(';').Length);
        Assert.Equal(7, CepCsv.Linha(Exemplo()).Split(';').Length);
        Assert.Equal("CEP;Logradouro;Bairro;Município;UF;Latitude;Longitude", CepCsv.Cabecalho);
    }

    /// <summary>Sem BOM o Excel pt-BR lê como ANSI e "Florianópolis" quebra.</summary>
    [Fact]
    public void o_arquivo_comeca_com_o_bom_utf8()
    {
        var bytes = CepCsv.Gerar([Exemplo()]);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Contains("Florianópolis", Texto(bytes));
    }

    /// <summary>
    /// A guarda do `InvariantGlobalization` do `Directory.Build.props`: com ele
    /// ligado, pedir a cultura pt-BR devolve a invariante EM SILÊNCIO e a
    /// coordenada sairia com ponto.
    /// </summary>
    [Fact]
    public void a_coordenada_sai_com_virgula_decimal()
    {
        Assert.Contains("-27,557615", CepCsv.Linha(Exemplo()));
        Assert.DoesNotContain("-27.557615", CepCsv.Linha(Exemplo()));
    }

    [Fact]
    public void o_cep_sai_formatado_e_como_texto()
    {
        Assert.StartsWith("88010-500;", CepCsv.Linha(Exemplo()));
    }

    /// <summary>`char(2)` no schema volta "SC " e não casa com "SC" em PROCV.</summary>
    [Fact]
    public void a_uf_sai_sem_o_enchimento_do_char2()
    {
        var c = Exemplo();
        c.Uf = "SC ";
        Assert.Contains(";SC;", CepCsv.Linha(c));
    }

    [Fact]
    public void campo_nulo_e_vazio_viram_celula_vazia()
    {
        var c = Exemplo();
        c.Logradouro = null;
        c.Localidade = "";   // os 86 CEPs sem nome de município na origem
        c.Lat = null;
        c.Lng = null;
        Assert.Equal("88010-500;;Centro;;SC;;", CepCsv.Linha(c));
    }

    [Fact]
    public void campo_com_o_separador_sai_entre_aspas()
    {
        var c = Exemplo();
        c.Logradouro = "Rua A; Fundos";
        Assert.Contains("\"Rua A; Fundos\"", CepCsv.Linha(c));
    }

    [Fact]
    public void aspas_no_campo_viram_aspas_dobradas()
    {
        var c = Exemplo();
        c.Logradouro = "Rua \"do\" Meio";
        Assert.Contains("\"Rua \"\"do\"\" Meio\"", CepCsv.Linha(c));
    }

    [Fact]
    public void quebra_de_linha_no_campo_nao_vaza_para_a_proxima_linha()
    {
        var c = Exemplo();
        c.Logradouro = "Rua A\nFundos";
        Assert.Contains("\"Rua A\nFundos\"", CepCsv.Linha(c));
    }

    /// <summary>
    /// Aspas não desarmam fórmula: o Excel tira as aspas na leitura e avalia
    /// `=`/`+`/`-`/`@` no começo da célula do mesmo jeito. Nenhuma das 40.445
    /// linhas de hoje dispara isto — a guarda é para o dia em que a base virar
    /// nacional, e o teste é para esse dia chegar com ela ainda de pé.
    /// </summary>
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1")]
    [InlineData("-")]
    [InlineData("@SUM(A1)")]
    public void campo_que_comeca_como_formula_e_desarmado(string valor)
    {
        var c = Exemplo();
        c.Logradouro = valor;
        Assert.Contains($";'{valor};", CepCsv.Linha(c));
    }

    /// <summary>
    /// As duas defesas juntas, na ordem certa: o apóstrofo entra ANTES do
    /// escape, então ele fica DENTRO das aspas. Se entrasse depois, ficaria
    /// fora e o campo inteiro deixaria de ser um campo.
    /// </summary>
    [Fact]
    public void formula_com_aspas_leva_as_duas_defesas()
    {
        var c = Exemplo();
        c.Logradouro = "=HYPERLINK(\"http://x\")";
        Assert.Contains(";\"'=HYPERLINK(\"\"http://x\"\")\";", CepCsv.Linha(c));
    }

    /// <summary>
    /// E a coordenada NÃO pode ser desarmada junto: ela começa com `-` e é
    /// número de verdade. Um apóstrofo ali a transformaria em texto, e a coluna
    /// deixaria de ordenar e de plotar.
    /// </summary>
    [Fact]
    public void a_coordenada_negativa_continua_numero()
    {
        Assert.EndsWith(";-27,557615;-48,495141", CepCsv.Linha(Exemplo()));
    }

    [Fact]
    public void as_linhas_terminam_em_crlf()
    {
        var texto = Texto(CepCsv.Gerar([Exemplo(), Exemplo()]));
        var partes = texto.Split("\r\n");
        // cabeçalho + 2 linhas + o vazio depois do CRLF final (RFC 4180: toda
        // linha termina, inclusive a última).
        Assert.Equal(4, partes.Length);
        Assert.Equal(string.Empty, partes[^1]);
        Assert.StartsWith("CEP;", texto.TrimStart('﻿'));
    }

    [Fact]
    public void o_arquivo_vazio_ainda_traz_o_cabecalho()
    {
        Assert.Equal("CEP;Logradouro;Bairro;Município;UF;Latitude;Longitude\r\n",
            Texto(CepCsv.Gerar([])).TrimStart('﻿'));
    }

    /// <summary>
    /// `*` e `?` são inválidos em nome de arquivo no Windows, e `88010-5x0` e
    /// `88010-5?0` são o MESMO padrão — dois nomes para o mesmo arquivo só
    /// atrapalham quem compara com o colega.
    /// </summary>
    [Theory]
    [InlineData("88xxx500", "ceps-88xxx500.csv")]
    [InlineData("88XXX500", "ceps-88xxx500.csv")]
    [InlineData("88010-5x0", "ceps-880105x0.csv")]
    [InlineData("88 xxx 500", "ceps-88xxx500.csv")]
    [InlineData("88*1?5_0", "ceps-88x1x5x0.csv")]
    [InlineData("8xxxxxxx", "ceps-8xxxxxxx.csv")]
    public void o_nome_do_arquivo_troca_todo_curinga_por_x(string padrao, string esperado)
    {
        Assert.Equal(esperado, CepCsv.NomeDoArquivo(padrao));
    }
}
