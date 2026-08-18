namespace TheDecrypter.Domain.Entities;

/// <summary>Papéis de acesso. Só dois: quem administra e quem usa.</summary>
public static class UserRoles
{
    public const string Admin = "admin";
    public const string User = "user";
}

/// <summary>
/// Situação da conta. O cadastro é aberto, mas entrar depende de aprovação
/// manual do admin — por isso `Pendente` é o estado inicial, e não um detalhe.
/// </summary>
public static class UserStatus
{
    public const string Pendente = "pendente";
    public const string Aprovado = "aprovado";
    public const string Bloqueado = "bloqueado";
}

/// <summary>Usuário do app: um identificador, uma senha e o direito de entrar.</summary>
public class AppUser
{
    public Guid Id { get; set; }

    /// <summary>
    /// O apelido — o identificador de quem se cadastra hoje. Sempre em
    /// minúscula, com índice único em `lower(nickname)`.
    ///
    /// Nulo nas contas anteriores ao apelido: elas continuam entrando pelo
    /// e-mail, e o índice único é PARCIAL justamente para deixar todas elas
    /// conviverem com nickname NULL.
    /// </summary>
    public string? Nickname { get; set; }

    /// <summary>
    /// Sempre em minúscula — ver o índice único em `lower(email)`.
    ///
    /// **Anulável de propósito**, e não por descuido: desde que o apelido
    /// existe, o e-mail é opcional. Se este tipo continuasse não-anulável, o EF
    /// marcaria a propriedade como obrigatória e estouraria ao materializar a
    /// primeira linha com e-mail NULL — derrubando a listagem inteira do painel
    /// de admin, não só aquela conta.
    /// </summary>
    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>PBKDF2 via <c>PasswordHasher</c>. Nunca sai da API.</summary>
    public string? PasswordHash { get; set; }

    public string Role { get; set; } = UserRoles.User;
    public string Status { get; set; } = UserStatus.Pendente;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }

    public bool IsAdmin => Role == UserRoles.Admin;

    /// <summary>
    /// Como esta conta se chama numa tela. Nunca vazio: sem apelido nem e-mail,
    /// uma linha da lista de aprovação viraria "Remover ?" — e o admin
    /// confirmaria uma exclusão sem saber de quem.
    /// </summary>
    public string Rotulo => Nickname ?? Email ?? $"conta {Id.ToString()[..8]}";
    public bool PodeEntrar => Status == UserStatus.Aprovado && PasswordHash is not null;
}
