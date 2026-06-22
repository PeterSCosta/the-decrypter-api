using TheDecrypter.Application.Cnpj;
using TheDecrypter.Domain.Gateways;
using TheDecrypter.Domain.Services.Cache;
using Xunit;

namespace TheDecrypter.Test;

public class CnpjServiceTests
{
    private sealed class FakeCache : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();
        public Task Remove(string key) { _store.Remove(key); return Task.CompletedTask; }
        public Task<T?> Get<T>(string key) =>
            Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);
        public Task<T> Set<T>(string key, T value, int a, int b) { _store[key] = value; return Task.FromResult(value); }
        public Task<IEnumerable<string>> GetKeyFromPrefix(string prefix) =>
            Task.FromResult<IEnumerable<string>>(_store.Keys.Where(k => k.Contains(prefix)).ToList());
    }

    private sealed class CountingGateway(CnpjInfo? info) : ICnpjGateway
    {
        public int Calls { get; private set; }
        public Task<CnpjInfo?> GetAsync(string cnpj, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(info);
        }
    }

    [Fact]
    public async Task LookupAsync_is_cache_first()
    {
        var info = new CnpjInfo("11222333000181", "ACME LTDA", null, "ATIVA",
            null, null, null, null, "SC", null, null);
        var gateway = new CountingGateway(info);
        var service = new CnpjService(new FakeCache(), gateway);

        var a = await service.LookupAsync("11.222.333/0001-81");
        var b = await service.LookupAsync("11222333000181");

        Assert.Equal("ACME LTDA", a!.RazaoSocial);
        Assert.Equal("ACME LTDA", b!.RazaoSocial);
        Assert.Equal(1, gateway.Calls); // a 2ª veio do cache, não do provedor
    }

    [Fact]
    public async Task LookupAsync_rejects_invalid_length()
    {
        var service = new CnpjService(new FakeCache(), new CountingGateway(null));
        Assert.Null(await service.LookupAsync("123"));
    }
}
