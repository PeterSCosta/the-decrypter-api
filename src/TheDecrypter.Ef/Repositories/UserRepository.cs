using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    public Task<AppUser?> ByApelidoAsync(string apelido, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Nickname == apelido, ct);

    public Task<AppUser?> ByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken ct = default) =>
        await db.Users.OrderBy(u => u.CreatedAt).ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => db.Users.CountAsync(ct);

    public async Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await SalvarTraduzindoConflito(ct);
    }

    public async Task UpdateAsync(AppUser user, CancellationToken ct = default)
    {
        db.Users.Update(user);
        await SalvarTraduzindoConflito(ct);
    }

    /// <summary>
    /// O índice único é o ÁRBITRO, e o erro dele precisa chegar como conflito.
    ///
    /// O serviço confere se o apelido existe antes de inserir, e isso resolve
    /// 99% dos casos com uma mensagem boa. Mas entre a conferência e o INSERT
    /// cabe outra requisição: duas pessoas escolhendo "peter" ao mesmo tempo
    /// passam as duas na conferência, e a segunda bate no
    /// `ux_app_user_nickname_lower`. Sem esta tradução isso subiria como 500 sem
    /// corpo — a tela diria "erro inesperado" para um problema que tem solução
    /// óbvia ("escolha outro").
    /// </summary>
    private async Task SalvarTraduzindoConflito(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            // 23505 = unique_violation. O nome do índice diz qual identificador
            // colidiu, e a mensagem muda com ele.
            var qual = pg.ConstraintName?.Contains("nickname") == true ? "apelido" : "e-mail";
            throw new InvalidOperationException(
                qual == "apelido"
                    ? "Esse apelido acabou de ser registrado por outra pessoa. Escolha outro."
                    : "Já existe uma conta com esse e-mail.");
        }
    }

    public async Task RemoveAsync(AppUser user, CancellationToken ct = default)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
    }
}
