namespace TheDecrypter.Domain.Http;

/// <summary>
/// De quem é esta requisição — a chave de partição do rate limiter.
///
/// Existe porque o limitador estava **silenciosamente desligado em produção**.
/// O `UseForwardedHeaders` usa `ForwardLimit = 1`, ou seja, lê só a entrada mais
/// à direita do `X-Forwarded-For`. Atrás da Cloudflare essa entrada é o IP da
/// **borda** dela, que muda de requisição para requisição: cada chamada abria
/// uma partição nova, nenhum contador acumulava, e 300 requisições seguidas
/// passaram sem um único 429. Em local, sem proxy nenhum, o IP era constante e
/// o limite funcionava — que é como isso passou despercebido.
///
/// A ordem de preferência abaixo vai do mais confiável ao menos:
/// `CF-Connecting-IP` é escrito pela Cloudflare e traz **um** endereço, sem
/// cadeia para interpretar errado.
/// </summary>
public static class ClientIp
{
    /// <summary>Cabeçalho que a Cloudflare escreve com o IP de origem.</summary>
    public const string CabecalhoCloudflare = "CF-Connecting-IP";

    /// <summary>
    /// Resolve o endereço do cliente a partir do que o proxy contou.
    ///
    /// **Ressalva honesta:** os dois cabeçalhos são texto que alguém pode
    /// forjar se alcançar a origem sem passar pela Cloudflare. Quem forjar
    /// consegue, no máximo, trocar de balde a cada tentativa — que é
    /// exatamente o comportamento de hoje, quando ninguém forja nada. Fechar
    /// isso de verdade é restringir a origem ao range da Cloudflare, no
    /// Traefik, e não aqui.
    /// </summary>
    public static string Resolver(string? cfConnectingIp, string? xForwardedFor, string? remoteIp)
    {
        var cf = (cfConnectingIp ?? string.Empty).Trim();
        if (cf.Length > 0) return cf;

        // Da esquerda: o primeiro da cadeia é o cliente; o que vem depois são os
        // proxies que a requisição atravessou. Pegar o último (o padrão do
        // ForwardLimit = 1) é justamente o que trouxe o IP da Cloudflare.
        var xff = xForwardedFor ?? string.Empty;
        foreach (var parte in xff.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidato = parte.Trim();
            if (candidato.Length > 0) return candidato;
        }

        var direto = (remoteIp ?? string.Empty).Trim();
        return direto.Length > 0 ? direto : "desconhecido";
    }
}
