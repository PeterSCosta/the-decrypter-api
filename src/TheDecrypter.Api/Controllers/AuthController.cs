using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TheDecrypter.Api.Auth;
using TheDecrypter.Application.Auth;
using TheDecrypter.Domain.Auth;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Api.Controllers;

/// <summary>
/// Cadastro e login.
///
/// **Sem `[OutputCache]` de propósito.** Todos os outros controllers herdam a
/// política "lookups" (10 min, em memória, compartilhada entre clientes) — numa
/// rota de sessão isso serviria a resposta de um usuário para outro.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth, ITokenService tokens) : ControllerBase
{
    /// <summary>
    /// Cria a conta. Nasce **pendente**: só entra depois que o admin aprova.
    ///
    /// Aqui o apelido é OBRIGATÓRIO — no serviço ele é opcional de propósito,
    /// porque o admin inicial nasce de variáveis de ambiente que podem trazer só
    /// o e-mail. Quem se cadastra pela tela escolhe apelido.
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Registrar([FromBody] NovoUsuarioDto novo, CancellationToken ct)
    {
        if (Apelido.Normalizar(novo.Apelido) is null)
            return BadRequest(new { message = "Escolha um apelido para entrar." });
        try
        {
            // `Admin: true` vindo do corpo é ignorado aqui: só o painel de admin
            // cria administrador. Sem isso, qualquer um se promoveria no cadastro.
            var criado = await auth.CadastrarAsync(novo with { Admin = false }, ct);
            return Ok(new
            {
                usuario = criado,
                message = "Cadastro recebido. Um administrador precisa aprovar seu acesso.",
            });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return Conflict(new { message = e.Message });
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Entrar([FromBody] CredenciaisDto cred, CancellationToken ct)
    {
        var (resultado, user) = await auth.AutenticarAsync(cred, ct);
        // Cada motivo tem uma saída diferente para quem está do outro lado:
        // esperar aprovação, falar com o admin, ou conferir a senha.
        if (resultado == ResultadoLogin.Pendente)
            return StatusCode(403, new { message = "Cadastro aguardando aprovação do administrador." });
        if (resultado == ResultadoLogin.Bloqueado)
            return StatusCode(403, new { message = "Acesso bloqueado. Fale com o administrador." });
        if (resultado != ResultadoLogin.Ok || user is null)
        {
            // A frase segue o MESMO teste do `@` que escolheu onde procurar:
            // quem digitou um e-mail lê "e-mail ou senha", quem digitou um
            // apelido lê "apelido ou senha". Não vaza nada — o `@` está no que a
            // própria pessoa acabou de escrever — e evita a dúvida de ler uma
            // mensagem sobre um campo que ela não usou.
            return Unauthorized(new
            {
                message = Apelido.PareceEmail(cred.Quem)
                    ? "E-mail ou senha inválidos."
                    : "Apelido ou senha inválidos.",
            });
        }

        var (token, expira) = tokens.Emitir(user);
        var dto = new UsuarioDto(
            user.Id, user.Nickname, user.Email, user.DisplayName, user.Role, user.Status,
            user.CreatedAt, user.ApprovedAt);
        return Ok(new SessaoDto(token, expira, dto));
    }

    /// <summary>Quem sou eu — o app usa para restaurar a sessão ao recarregar.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Eu(CancellationToken ct)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(id, out var guid)) return Unauthorized(new { message = "sessão inválida" });
        var dto = await auth.PorIdAsync(guid, ct);
        // A conta pode ter sido removida ou bloqueada depois do token emitido.
        if (dto is null || dto.Situacao != Domain.Entities.UserStatus.Aprovado)
            return Unauthorized(new { message = "sessão inválida" });
        return Ok(dto);
    }
}
