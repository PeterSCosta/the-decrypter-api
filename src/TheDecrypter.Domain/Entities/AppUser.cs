namespace TheDecrypter.Domain.Entities;

/// <summary>Usuário (esqueleto para controle de acesso/limite por usuário no futuro).</summary>
public class AppUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
