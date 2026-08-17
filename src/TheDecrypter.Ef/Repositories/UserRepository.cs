using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Repositories;

namespace TheDecrypter.Ef.Repositories;

/// <summary>
/// Único repositório que escreve. O contexto roda com <c>NoTracking</c> global
/// (a API é quase toda leitura), então as alterações têm de ser anexadas
/// explicitamente — sem o <c>Update</c>/<c>Remove</c> abaixo o EF não tem o que
/// salvar e o <c>SaveChanges</c> passa em silêncio.
/// </summary>
public class UserRepository(DecrypterDbContext db) : IUserRepository
{
    public Task<AppUser?> ByEmailAsync(string email, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<AppUser?> ByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken ct = default) =>
        await db.Users.OrderBy(u => u.CreatedAt).ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => db.Users.CountAsync(ct);

    public async Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AppUser user, CancellationToken ct = default)
    {
        db.Users.Update(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(AppUser user, CancellationToken ct = default)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
    }
}
