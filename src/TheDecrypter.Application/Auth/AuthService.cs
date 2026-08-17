using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Gateways;
using TheDecrypter.Domain.Repositories;

namespace TheDecrypter.Application.Auth;

public interface IAuthService
{
    Task<UsuarioDto> CadastrarAsync(NovoUsuarioDto novo, CancellationToken ct = default);
    Task<(ResultadoLogin Resultado, AppUser? Usuario)> AutenticarAsync(
        CredenciaisDto cred, CancellationToken ct = default);
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken ct = default);
    Task<UsuarioDto?> AjustarAsync(Guid id, AjusteUsuarioDto ajuste, Guid porQuem, CancellationToken ct = default);
    Task<bool> RemoverAsync(Guid id, Guid porQuem, CancellationToken ct = default);
    Task<UsuarioDto?> PorIdAsync(Guid id, CancellationToken ct = default);
    Task GarantirAdminInicialAsync(string? email, string? senha, CancellationToken ct = default);
}

/// <summary>
/// Cadastro, login e administração de usuários.
///
/// O cadastro é aberto e **não valida e-mail**: quem entra depende de aprovação
/// manual do admin, então o e-mail é só um identificador, não uma prova. Isso é
/// decisão de produto, não descuido — a barreira é a aprovação.
/// </summary>
public class AuthService(
    IUserRepository repo,
    IPasswordHasher<AppUser> hasher,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>
    /// Minúscula e sem espaço. O índice `ux_app_user_email_lower` é a rede de
    /// segurança, mas normalizar aqui é o que evita a conta duplicada.
    /// </summary>
    private static string Normaliza(string email) => email.Trim().ToLowerInvariant();

    private static UsuarioDto ParaDto(AppUser u) =>
        new(u.Id, u.Email, u.DisplayName, u.Role, u.Status, u.CreatedAt, u.ApprovedAt);

    public async Task<UsuarioDto> CadastrarAsync(NovoUsuarioDto novo, CancellationToken ct = default)
    {
        var email = Normaliza(novo.Email);
        if (email.Length < 3 || !email.Contains('@'))
            throw new ArgumentException("informe um e-mail válido");
        if (novo.Senha.Length < 8)
            throw new ArgumentException("a senha precisa de pelo menos 8 caracteres");
        if (await repo.ByEmailAsync(email, ct) is not null)
            throw new InvalidOperationException("já existe uma conta com esse e-mail");

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(novo.Nome) ? null : novo.Nome.Trim(),
            Role = novo.Admin ? UserRoles.Admin : UserRoles.User,
            // Quem o admin cria já entra liberado; quem se cadastra sozinho espera.
            Status = novo.Admin ? UserStatus.Aprovado : UserStatus.Pendente,
            CreatedAt = DateTimeOffset.UtcNow,
            ApprovedAt = novo.Admin ? DateTimeOffset.UtcNow : null,
        };
        user.PasswordHash = hasher.HashPassword(user, novo.Senha);
        await repo.AddAsync(user, ct);
        return ParaDto(user);
    }

    public async Task<(ResultadoLogin, AppUser?)> AutenticarAsync(
        CredenciaisDto cred, CancellationToken ct = default)
    {
        var user = await repo.ByEmailAsync(Normaliza(cred.Email), ct);
        if (user?.PasswordHash is null) return (ResultadoLogin.CredencialInvalida, null);

        var v = hasher.VerifyHashedPassword(user, user.PasswordHash, cred.Senha);
        if (v == PasswordVerificationResult.Failed) return (ResultadoLogin.CredencialInvalida, null);

        // O hasher pede re-hash quando o algoritmo/iterações mudam de versão.
        if (v == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, cred.Senha);
            await repo.UpdateAsync(user, ct);
        }

        return user.Status switch
        {
            UserStatus.Aprovado => (ResultadoLogin.Ok, user),
            UserStatus.Bloqueado => (ResultadoLogin.Bloqueado, null),
            _ => (ResultadoLogin.Pendente, null),
        };
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken ct = default) =>
        [.. (await repo.ListAsync(ct)).Select(ParaDto)];

    public async Task<UsuarioDto?> PorIdAsync(Guid id, CancellationToken ct = default) =>
        await repo.ByIdAsync(id, ct) is { } u ? ParaDto(u) : null;

    public async Task<UsuarioDto?> AjustarAsync(
        Guid id, AjusteUsuarioDto ajuste, Guid porQuem, CancellationToken ct = default)
    {
        var user = await repo.ByIdAsync(id, ct);
        if (user is null) return null;

        if (ajuste.Situacao is { } s)
        {
            if (s is not (UserStatus.Pendente or UserStatus.Aprovado or UserStatus.Bloqueado))
                throw new ArgumentException("situação inválida");
            // Um admin que se bloqueia sozinho tranca a porta com a chave dentro:
            // ninguém mais aprova ninguém.
            if (user.Id == porQuem && s != UserStatus.Aprovado)
                throw new InvalidOperationException("você não pode bloquear a própria conta");
            user.Status = s;
            if (s == UserStatus.Aprovado && user.ApprovedAt is null)
            {
                user.ApprovedAt = DateTimeOffset.UtcNow;
                user.ApprovedBy = porQuem;
            }
        }

        if (ajuste.Papel is { } p)
        {
            if (p is not (UserRoles.Admin or UserRoles.User))
                throw new ArgumentException("papel inválido");
            if (user.Id == porQuem && p != UserRoles.Admin)
                throw new InvalidOperationException("você não pode tirar o próprio acesso de admin");
            user.Role = p;
        }

        if (ajuste.Nome is { } n) user.DisplayName = string.IsNullOrWhiteSpace(n) ? null : n.Trim();

        if (ajuste.Senha is { } senha)
        {
            if (senha.Length < 8) throw new ArgumentException("a senha precisa de pelo menos 8 caracteres");
            user.PasswordHash = hasher.HashPassword(user, senha);
        }

        await repo.UpdateAsync(user, ct);
        return ParaDto(user);
    }

    public async Task<bool> RemoverAsync(Guid id, Guid porQuem, CancellationToken ct = default)
    {
        if (id == porQuem) throw new InvalidOperationException("você não pode remover a própria conta");
        var user = await repo.ByIdAsync(id, ct);
        if (user is null) return false;
        await repo.RemoveAsync(user, ct);
        return true;
    }

    /// <summary>
    /// Cria o primeiro admin a partir da configuração, se ele não existir.
    ///
    /// Sem isto não há como começar: o cadastro nasce pendente e só um admin
    /// aprova, então uma base zerada ficaria travada para sempre. É idempotente
    /// — roda em todo boot e não mexe em quem já está lá.
    /// </summary>
    public async Task GarantirAdminInicialAsync(string? email, string? senha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            if (await repo.CountAsync(ct) == 0)
            {
                logger.LogWarning(
                    "Nenhum usuário cadastrado e Admin__Email/Admin__Senha não definidos: " +
                    "ninguém consegue entrar nem aprovar. Defina as duas variáveis e suba de novo.");
            }
            return;
        }

        var alvo = Normaliza(email);
        if (await repo.ByEmailAsync(alvo, ct) is not null) return;

        await CadastrarAsync(new NovoUsuarioDto(alvo, senha, "Administrador", Admin: true), ct);
        logger.LogInformation("Admin inicial criado: {Email}", alvo);
    }
}
