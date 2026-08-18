using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheDecrypter.Application.Auth;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Api.Controllers;

/// <summary>
/// Painel do admin: listar, criar, aprovar/bloquear e remover usuários.
/// Sem `[OutputCache]` — a lista muda a cada aprovação (ver `AuthController`).
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = UserRoles.Admin)]
public class AdminUsersController(IAuthService auth) : ControllerBase
{
    private Guid Eu =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var g)
            ? g
            : Guid.Empty;

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var hits = await auth.ListarAsync(ct);
        return Ok(new { total = hits.Count, hits });
    }

    /// <summary>
    /// Cria a conta pelo painel — **já liberada**.
    ///
    /// A fila de aprovação existe para o cadastro ABERTO, onde qualquer um
    /// aparece. Aqui quem digitou o apelido e a senha foi o próprio
    /// administrador, e mandar a conta para a fila dele mesmo era o defeito que
    /// quebrava o caso real: o admin criava, passava a senha para a pessoa, e
    /// ela batia em "Cadastro aguardando aprovação do administrador".
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] NovoUsuarioDto novo, CancellationToken ct)
    {
        try
        {
            return Ok(await auth.CadastrarAsync(novo with { Aprovado = true }, ct));
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

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Ajustar(
        Guid id, [FromBody] AjusteUsuarioDto ajuste, CancellationToken ct)
    {
        try
        {
            var dto = await auth.AjustarAsync(id, ajuste, Eu, ct);
            return dto is null ? NotFound(new { message = "usuário não encontrado" }) : Ok(dto);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id, CancellationToken ct)
    {
        try
        {
            return await auth.RemoverAsync(id, Eu, ct)
                ? NoContent()
                : NotFound(new { message = "usuário não encontrado" });
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }
}
