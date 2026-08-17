using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Domain.Repositories;

/// <summary>Acesso à tabela de usuários. E-mail sempre normalizado pelo chamador.</summary>
public interface IUserRepository
{
    Task<AppUser?> ByEmailAsync(string email, CancellationToken ct = default);
    Task<AppUser?> ByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
    Task UpdateAsync(AppUser user, CancellationToken ct = default);
    Task RemoveAsync(AppUser user, CancellationToken ct = default);
}
