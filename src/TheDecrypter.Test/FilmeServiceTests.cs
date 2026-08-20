using TheDecrypter.Application.Lookups;
using TheDecrypter.Domain.Gateways;
using TheDecrypter.Domain.Search;
using TheDecrypter.Domain.Services.Cache;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// O TESTE QUE FALTAVA, e a falha que ele teria pego.
///
/// Havia dois lugares conferindo a forma da chave de filme: o portão
/// (`LookupShape`) e o serviço (`LookupService.FilmeAsync`). Quando o `Q4941`
/// virou segunda porta, o portão aprendeu e o serviço não — e o resultado foi
/// o pior tipo de falha, porque **nada quebrou**: a bancada abria a consulta,
/// o serviço a descartava antes de sair, e a tela mostrava só a leitura de
/// coordenada. Nenhum erro, nenhum log, nenhum teste vermelho.
///
/// Testar `LookupShape` sozinho não pegava: o portão estava certo. O que faltava
/// era afirmar que a chave ATRAVESSA — que o gateway é de fato chamado.
/// </summary>
public class FilmeServiceTests
{
    private sealed class CacheFalso : ICacheService
    {
        private readonly Dictionary<string, object?> _itens = new();
        public Task Remove(string key) { _itens.Remove(key); return Task.CompletedTask; }
        public Task<T?> Get<T>(string key) =>
            Task.FromResult(_itens.TryGetValue(key, out var v) ? (T?)v : default);
        public Task<T> Set<T>(string key, T value, int a, int b) { _itens[key] = value; return Task.FromResult(value); }
        public Task<IEnumerable<string>> GetKeyFromPrefix(string p) =>
            Task.FromResult<IEnumerable<string>>(_itens.Keys.Where(k => k.Contains(p)).ToList());
    }

    private sealed class WikidataQueAnota : IWikidataGateway
    {
        public List<string> Recebeu { get; } = [];
        public Task<FilmeInfo?> FilmePorImdbAsync(string chave, CancellationToken ct = default)
        {
            Recebeu.Add(chave);
            return Task.FromResult<FilmeInfo?>(Ficha(chave));
        }

        public Task<ResolucaoWikidata> ResolverAsync(string chave, CancellationToken ct = default)
        {
            Recebeu.Add(chave);
            return Task.FromResult(new ResolucaoWikidata(Ficha(chave), null));
        }

        private static FilmeInfo Ficha(string chave) =>
            new(chave, null, null, "Skyfall", "Skyfall", 2012, 143, null, null, null, "Q4941", "Wikidata");
    }

    private static (LookupService svc, WikidataQueAnota gw) Montar()
    {
        var gw = new WikidataQueAnota();
        return (new LookupService(new CacheFalso(), null!, null!, null!, null!, null!, null!, gw), gw);
    }

    /// <summary>A afirmação central: as DUAS formas chegam ao gateway.</summary>
    [Theory]
    [InlineData("tt1074638", "tt1074638")]
    [InlineData("TT1074638", "tt1074638")]
    [InlineData("Q4941", "Q4941")]
    [InlineData("q4941", "Q4941")]
    [InlineData("  Q4941  ", "Q4941")]
    public async Task A_chave_atravessa_ate_o_gateway(string entrada, string esperado)
    {
        var (svc, gw) = Montar();
        var r = await svc.WikidataAsync(entrada);
        Assert.NotNull(r.Filme);
        Assert.Equal([esperado], gw.Recebeu);
    }

    /// <summary>E o que não é chave não gasta requisição.</summary>
    [Theory]
    [InlineData("4941")]
    [InlineData("Q0")]
    [InlineData("tt111")]
    [InlineData("Bacurau")]
    [InlineData("")]
    public async Task O_que_nao_e_chave_nao_chega_ao_gateway(string entrada)
    {
        var (svc, gw) = Montar();
        Assert.Null((await svc.WikidataAsync(entrada)).Filme);
        Assert.Empty(gw.Recebeu);
    }

    /// <summary>
    /// O portão e o serviço não podem divergir — foi a divergência entre os dois
    /// que produziu a falha silenciosa. Isto amarra os dois ao mesmo julgamento.
    /// </summary>
    [Theory]
    [InlineData("tt1074638")]
    [InlineData("Q4941")]
    [InlineData("Q220741")]
    [InlineData("4941")]
    [InlineData("Q0")]
    [InlineData("Bacurau")]
    public async Task O_portao_e_o_servico_concordam_sempre(string entrada)
    {
        var (svc, gw) = Montar();
        await svc.WikidataAsync(entrada);
        var portaoAbriu = LookupShape.De(entrada).HasFlag(Consultas.Filme);
        var servicoPerguntou = gw.Recebeu.Count > 0;
        Assert.Equal(portaoAbriu, servicoPerguntou);
    }

    [Fact]
    public async Task A_segunda_chamada_vem_do_cache_e_nao_gasta_requisicao()
    {
        var (svc, gw) = Montar();
        await svc.WikidataAsync("Q4941");
        await svc.WikidataAsync("q4941");
        Assert.Single(gw.Recebeu);
    }
}
