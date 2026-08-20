using TheDecrypter.Domain.Search;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// O campo que separa "não achei" de "não sei procurar isto".
///
/// A resposta de `/api/lookup` para uma entrada sem forma reconhecida é byte a
/// byte igual à de uma busca que não bateu em nada — todas as chaves em `null`.
/// `Nomes` é o que quebra esse empate, e o valor dele está em o **vazio ser
/// garantidamente vazio**: uma lista que sempre traz alguma coisa reintroduz
/// exatamente a mentira que ela existe para matar.
/// </summary>
public class LookupNomesTests
{
    [Fact]
    public void Sem_forma_reconhecida_a_lista_e_vazia()
    {
        Assert.Empty(LookupShape.Nomes(LookupShape.De("...")));
        Assert.Empty(LookupShape.Nomes(LookupShape.De("")));
        // `MR-103` é chapa de estação geodésica: a bancada resolve local, o
        // servidor não tem o que consultar. É o contraexemplo que derruba a
        // ideia de o cliente adivinhar a forma sozinho.
        Assert.Empty(LookupShape.Nomes(LookupShape.De("MR-103")));
    }

    [Fact]
    public void Nomeia_o_que_de_fato_abriu()
    {
        Assert.Contains("CepExato", LookupShape.Nomes(LookupShape.De("89010000")));
        Assert.Contains("Aeroporto", LookupShape.Nomes(LookupShape.De("GRU")));
        Assert.Equal(["Filme"], LookupShape.Nomes(LookupShape.De("tt0111161")));
    }

    /// <summary>
    /// Uma entrada que abre várias portas nomeia TODAS. Numa lista de sessenta
    /// itens, "perguntei em rua e no nome de doença — nenhuma bateu" é uma
    /// resposta útil; "não achei" sozinho não é.
    /// </summary>
    [Fact]
    public void Entrada_que_abre_varias_portas_nomeia_todas()
    {
        var nomes = LookupShape.Nomes(LookupShape.De("Bacurau"));
        Assert.Contains("RuaOuBairro", nomes);
        Assert.Contains("CidNome", nomes);
    }

    [Fact]
    public void Nunca_nomeia_Nenhuma()
    {
        foreach (var termo in new[] { "", "...", "89010000", "GRU", "tt0111161", "Bacurau", "4202404" })
            Assert.DoesNotContain("Nenhuma", LookupShape.Nomes(LookupShape.De(termo)));
    }

    /// <summary>
    /// A lista é a leitura fiel das bandeiras — se uma consulta nova entrar no
    /// enum e não aparecer aqui, é porque alguém escreveu um mapa à mão.
    /// </summary>
    [Fact]
    public void Toda_bandeira_do_enum_e_nomeavel()
    {
        foreach (var f in Enum.GetValues<Consultas>())
        {
            if (f == Consultas.Nenhuma) continue;
            Assert.Equal([f.ToString()], LookupShape.Nomes(f));
        }
    }
}

/// <summary>
/// A GRAFIA CANÔNICA DO CEP. `88010-500` é como o CEP se escreve, e era o único
/// jeito de escrever CEP que o portão não reconhecia — a resposta saía "não abri
/// consulta nenhuma". Numa lista de CEPs colados, toda linha dizia "não sei
/// procurar isto".
/// </summary>
public class LookupShapeCepEscritoTests
{
    [Theory]
    [InlineData("88010-500")]
    [InlineData("88.010-500")]
    [InlineData("89010-000")]
    [InlineData("01310-100")]
    [InlineData("88010.500")]
    public void Cep_com_mascara_abre_a_consulta_de_cep(string escrito)
    {
        Assert.True(LookupShape.De(escrito).HasFlag(Consultas.CepExato), escrito);
        Assert.Contains("CepExato", LookupShape.Nomes(LookupShape.De(escrito)));
    }

    [Fact]
    public void Com_e_sem_mascara_abrem_a_mesma_porta()
    {
        Assert.True(LookupShape.De("88010500").HasFlag(Consultas.CepExato));
        Assert.True(LookupShape.De("88010-500").HasFlag(Consultas.CepExato));
    }

    /// <summary>
    /// O corte é EXATAMENTE oito dígitos, e é por isso: uma coordenada tem
    /// ponto e hífen, e abriria consulta de CEP e de município a cada linha de
    /// uma lista de coordenadas — requisição gasta num balde compartilhado.
    /// </summary>
    [Theory]
    [InlineData("-26.9194")]
    [InlineData("-49.0661")]
    [InlineData("26.91")]
    public void Coordenada_nao_vira_consulta_de_cep(string coord)
    {
        Assert.False(LookupShape.De(coord).HasFlag(Consultas.CepExato), coord);
        Assert.False(LookupShape.De(coord).HasFlag(Consultas.Municipio), coord);
    }

    [Fact]
    public void Texto_com_letra_nao_entra_por_aqui()
    {
        Assert.False(LookupShape.De("CEP 88010-500").HasFlag(Consultas.CepExato));
    }
}
