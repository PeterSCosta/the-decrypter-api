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

/// <summary>Usuário do app: e-mail, senha e o direito de entrar.</summary>
public class AppUser
{
    public Guid Id { get; set; }

    /// <summary>Sempre em minúscula — ver o índice único em `lower(email)`.</summary>
    public string Email { get; set; } = default!;

    public string? DisplayName { get; set; }

    /// <summary>PBKDF2 via <c>PasswordHasher</c>. Nunca sai da API.</summary>
    public string? PasswordHash { get; set; }

    public string Role { get; set; } = UserRoles.User;
    public string Status { get; set; } = UserStatus.Pendente;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }

    public bool IsAdmin => Role == UserRoles.Admin;
    public bool PodeEntrar => Status == UserStatus.Aprovado && PasswordHash is not null;
}
