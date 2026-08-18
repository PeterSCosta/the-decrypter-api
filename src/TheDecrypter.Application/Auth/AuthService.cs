using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TheDecrypter.Domain.Auth;
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
    Task GarantirAdminInicialAsync(
        string? email, string? senha, string? apelido = null, CancellationToken ct = default);
}

/// <summary>
/// Cadastro, login e administração de usuários.
///
/// O cadastro é aberto e **não valida identificador nenhum**: quem entra depende
/// de aprovação manual do admin, então apelido e e-mail são etiquetas, não
/// provas. Isso é decisão de produto, não descuido — a barreira é a aprovação.
///
/// ── DOIS IDENTIFICADORES, UM CAMPO ──────────────────────────────────────────
/// Quem se cadastra hoje escolhe um APELIDO; o e-mail virou opcional. Quem já
/// tinha conta continua entrando pelo e-mail, sem fazer nada — é por isso que
/// nada foi migrado à força: preencher o apelido com o "antes do @" colidiria
/// entre pessoas de domínios diferentes e trocaria o identificador de alguém
/// sem avisar.
///
/// A conta precisa de PELO MENOS UM dos dois. Sem essa invariante, um ajuste no
/// painel podia deixar a conta sem forma nenhuma de entrar — e ninguém
/// perceberia até a pessoa tentar.
/// </summary>
public class AuthService(
    IUserRepository repo,
    IPasswordHasher<AppUser> hasher,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>
    /// Minúscula e sem espaço. O índice `ux_app_user_email_lower` é a rede de
    /// segurança, mas normalizar aqui é o que evita a conta duplicada.
    ///
    /// Branco vira `null`, nunca string vazia: duas contas sem e-mail gravadas
    /// como `''` colidiriam no índice único — e a segunda viraria um 500 sem
    /// explicação, num campo que agora é opcional.
    /// </summary>
    private static string? NormalizaEmail(string? email)
    {
        var t = (email ?? string.Empty).Trim().ToLowerInvariant();
        return t.Length == 0 ? null : t;
    }

    /// <summary>
    /// O nome de exibição é texto livre — mas não texto QUALQUER.
    ///
    /// É ele que o painel de aprovação mostra, e o admin decide olhando essa
    /// linha. Sem regra, alguém se cadastra com o nome "peter@empresa.com" e o
    /// admin aprova achando que é outra pessoa. Proibir `@` e limitar o tamanho
    /// fecha a imitação no único lugar onde ela teria efeito.
    /// </summary>
    private static string? NormalizaNome(string? nome)
    {
        var t = (nome ?? string.Empty).Trim();
        if (t.Length == 0) return null;
        if (t.Length > 60) throw new ArgumentException("O nome pode ter até 60 caracteres.");
        if (t.Contains('@')) throw new ArgumentException("O nome não pode conter @.");
        // Controle e marcas de direção (bidi) reescrevem a linha na tela sem
        // aparecer: "Ana\u202Emoc.ossap" se lê ao contrário no painel.
        if (t.Any(c => char.IsControl(c) || (c >= '\u202A' && c <= '\u202E')))
            throw new ArgumentException("O nome tem caracteres invisíveis. Escreva só texto.");
        return t;
    }

    private static UsuarioDto ParaDto(AppUser u) =>
        new(u.Id, u.Nickname, u.Email, u.DisplayName, u.Role, u.Status, u.CreatedAt, u.ApprovedAt);

    /// <summary>
    /// Confere o apelido e devolve a forma normalizada. Lança com a mensagem
    /// pronta para a tela — formato, reservado ou já em uso.
    /// </summary>
    private async Task<string?> ConfereApelidoAsync(
        string? bruto, Guid? donoAtual, CancellationToken ct)
    {
        var apelido = Apelido.Normalizar(bruto);
        if (apelido is null) return null;

        if (!Apelido.Valido(apelido)) throw new ArgumentException(Apelido.RegraEmPalavras);
        if (Apelido.EhReservado(apelido))
            throw new ArgumentException("Esse apelido é reservado. Escolha outro.");

        var jaTem = await repo.ByApelidoAsync(apelido, ct);
        // `donoAtual` deixa a pessoa regravar o próprio apelido sem conflito —
        // trocar só a caixa ("Peter" → "peter") não pode ser recusado como se
        // fosse de outra pessoa.
        if (jaTem is not null && jaTem.Id != donoAtual)
            throw new InvalidOperationException("Esse apelido já está em uso. Escolha outro.");

        return apelido;
    }

    public async Task<UsuarioDto> CadastrarAsync(NovoUsuarioDto novo, CancellationToken ct = default)
    {
        var apelido = await ConfereApelidoAsync(novo.Apelido, null, ct);
        var email = NormalizaEmail(novo.Email);

        // Um dos dois, no mínimo. Uma conta sem identificador nenhum não é conta:
        // ninguém consegue entrar nela, nem hoje nem depois.
        if (apelido is null && email is null)
            throw new ArgumentException("Escolha um apelido para entrar.");
        if (email is not null && (email.Length < 3 || !email.Contains('@')))
            throw new ArgumentException("Informe um e-mail válido, ou deixe o campo em branco.");
        if (novo.Senha.Length < 8)
            throw new ArgumentException("A senha precisa de pelo menos 8 caracteres.");
        if (email is not null && await repo.ByEmailAsync(email, ct) is not null)
            throw new InvalidOperationException("Já existe uma conta com esse e-mail.");

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Nickname = apelido,
            Email = email,
            DisplayName = NormalizaNome(novo.Nome),
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
        // O ROTEADOR: `@` significa e-mail e nada mais significa. Como o apelido
        // proíbe `@` e todo e-mail o contém, os dois conjuntos são disjuntos —
        // uma busca indexada de cada vez, sem `OR` e sem chance de duas contas
        // casarem com o mesmo texto.
        var quem = cred.Quem;
        var user = Apelido.PareceEmail(quem)
            ? await repo.ByEmailAsync(quem.ToLowerInvariant(), ct)
            : await repo.ByApelidoAsync(quem.ToLowerInvariant(), ct);
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

        if (ajuste.Nome is { } n) user.DisplayName = NormalizaNome(n);

        if (ajuste.Apelido is { } a)
        {
            var apelido = await ConfereApelidoAsync(a, user.Id, ct);
            // A mesma invariante do cadastro, agora do lado que consegue
            // desfazê-la: apagar o apelido de uma conta sem e-mail trancaria a
            // pessoa para fora para sempre, e sem nenhum aviso.
            if (apelido is null && user.Email is null)
                throw new InvalidOperationException(
                    "Esta conta não tem e-mail: sem apelido, ninguém entra nela.");
            user.Nickname = apelido;
        }

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
    public async Task GarantirAdminInicialAsync(
        string? email, string? senha, string? apelido = null, CancellationToken ct = default)
    {
        var alvoEmail = NormalizaEmail(email);
        var alvoApelido = Apelido.Normalizar(apelido);

        if (string.IsNullOrWhiteSpace(senha) || (alvoEmail is null && alvoApelido is null))
        {
            if (await repo.CountAsync(ct) == 0)
            {
                logger.LogWarning(
                    "Nenhum usuário cadastrado e Admin__Senha + (Admin__Apelido ou Admin__Email) " +
                    "não definidos: ninguém consegue entrar nem aprovar. Defina e suba de novo.");
            }
            return;
        }

        // Procura pelos DOIS identificadores antes de decidir criar. Em produção
        // só existe `Admin__Email`, e o admin já está lá: sem esta busca, definir
        // `Admin__Apelido` depois criaria uma SEGUNDA conta de administrador a
        // cada boot — ou estouraria no índice único do e-mail.
        var existente = alvoApelido is not null ? await repo.ByApelidoAsync(alvoApelido, ct) : null;
        existente ??= alvoEmail is not null ? await repo.ByEmailAsync(alvoEmail, ct) : null;

        if (existente is not null)
        {
            // Backfill: o admin que já existia ganha o apelido novo sem perder
            // nada — nem a senha, nem o e-mail com que sempre entrou.
            if (alvoApelido is not null && existente.Nickname is null)
            {
                existente.Nickname = alvoApelido;
                await repo.UpdateAsync(existente, ct);
                logger.LogInformation("Apelido do admin definido: {Apelido}", alvoApelido);
            }
            return;
        }

        await CadastrarAsync(
            new NovoUsuarioDto(senha, alvoApelido, alvoEmail, "Administrador", Admin: true), ct);
        logger.LogInformation("Admin inicial criado: {Quem}", alvoApelido ?? alvoEmail);
    }
}
