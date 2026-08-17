using TheDecrypter.Domain.Http;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>
/// O caso que importa é o penúltimo: era ele que desligava o rate limiter em
/// produção sem nenhum sinal — a API respondia 200 a tudo, saudável, sem limite.
/// </summary>
public class ClientIpTests
{
    [Fact]
    public void Cloudflare_tem_precedencia()
    {
        Assert.Equal(
            "203.0.113.7",
            ClientIp.Resolver("203.0.113.7", "198.51.100.1, 172.20.0.5", "172.20.0.5"));
    }

    [Fact]
    public void Sem_cloudflare_pega_o_primeiro_do_encadeamento()
    {
        // O cliente é o da ESQUERDA; o resto são os proxies atravessados.
        Assert.Equal(
            "198.51.100.1",
            ClientIp.Resolver(null, "198.51.100.1, 172.20.0.5, 10.0.0.2", "10.0.0.2"));
    }

    [Fact]
    public void Sem_proxy_nenhum_usa_a_conexao()
    {
        Assert.Equal("192.0.2.9", ClientIp.Resolver(null, null, "192.0.2.9"));
        Assert.Equal("192.0.2.9", ClientIp.Resolver("", "  ", "192.0.2.9"));
    }

    [Fact]
    public void Mesmo_cliente_por_bordas_diferentes_da_cloudflare_cai_no_mesmo_balde()
    {
        // ESTE é o bug. As três requisições são da mesma pessoa, e chegaram por
        // três bordas diferentes da Cloudflare. Lendo a entrada mais à direita
        // (o padrão de `ForwardLimit = 1`), cada uma abria uma partição própria
        // e o contador nunca passava de 1 — 300 requisições, zero 429.
        var chaves = new[]
        {
            ClientIp.Resolver(null, "203.0.113.7, 172.68.1.1", "172.68.1.1"),
            ClientIp.Resolver(null, "203.0.113.7, 162.158.2.2", "162.158.2.2"),
            ClientIp.Resolver(null, "203.0.113.7, 104.23.3.3", "104.23.3.3"),
        };
        Assert.Single(chaves.Distinct());
        Assert.Equal("203.0.113.7", chaves[0]);
    }

    [Fact]
    public void Sem_nada_nao_devolve_vazio()
    {
        // String vazia como chave de partição funde clientes distintos no mesmo
        // balde por acidente; um rótulo explícito deixa isso visível.
        Assert.Equal("desconhecido", ClientIp.Resolver(null, null, null));
    }
}
