namespace TheDecrypter.Api.Middleware;

/// <summary>
/// Converte falhas transitórias de provedores externos em <c>503</c> limpo, em vez
/// de deixar virar <c>500</c>. Cobre <see cref="HttpRequestException"/> e as exceções
/// do Polly (timeout, circuit-breaker, rate-limiter — todas no namespace "Polly.*"),
/// que escapam do try/catch dos controllers. Centraliza o tratamento p/ toda a API.
/// </summary>
public sealed class UpstreamErrorMiddleware(RequestDelegate next, ILogger<UpstreamErrorMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex) when (IsTransientUpstream(ex) && !ctx.Response.HasStarted)
        {
            log.LogWarning(ex, "Provedor externo transitório → 503 ({path})", ctx.Request.Path);
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(
                new { message = "Provedor externo indisponível. Tente novamente." });
        }
    }

    // HttpRequestException + qualquer exceção do Polly (Polly.Timeout.TimeoutRejectedException,
    // Polly.CircuitBreaker.BrokenCircuitException, Polly.RateLimiting.RateLimiterRejectedException,
    // todas derivam de Polly.ExecutionRejectedException no namespace "Polly"). Checar o namespace
    // evita uma referência de compilação direta ao pacote Polly aqui.
    private static bool IsTransientUpstream(Exception ex) =>
        ex is HttpRequestException or TimeoutException
        || ex.GetType().Namespace?.StartsWith("Polly", StringComparison.Ordinal) == true;
}
