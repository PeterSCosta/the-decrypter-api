using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using TheDecrypter.Application.Auth;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Gateways;
using TheDecrypter.Domain.Repositories;
using Xunit;

namespace TheDecrypter.Test;

/// <summary>Repositório em memória — o projeto não usa lib de mock.</summary>
internal sealed class FakeUsers : IUserRepository
{
    private readonly List<AppUser> _users = [];

    public Task<AppUser?> ByEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Email == email));

    public Task<AppUser?> ByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AppUser>>(_users);

    public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(_users.Count);

    public Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AppUser user, CancellationToken ct = default) => Task.CompletedTask;

    public Task RemoveAsync(AppUser user, CancellationToken ct = default)
    {
        _users.Remove(user);
        return Task.CompletedTask;
    }
}

public class AuthServiceTests
{
    private static (AuthService Svc, FakeUsers Repo) Montar()
    {
        var repo = new FakeUsers();
        return (new AuthService(repo, new PasswordHasher<AppUser>(),
            NullLogger<AuthService>.Instance), repo);
    }

    [Fact]
    public async Task cadastro_nasce_pendente_e_nao_entra()
    {
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("a@b.com", "senha12345", "A"));
        Assert.Equal(UserStatus.Pendente, novo.Situacao);

        var (resultado, user) = await svc.AutenticarAsync(new CredenciaisDto("a@b.com", "senha12345"));
        Assert.Equal(ResultadoLogin.Pendente, resultado);
        Assert.Null(user);
    }

    [Fact]
    public async Task depois_de_aprovado_entra()
    {
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("a@b.com", "senha12345", null));
        await svc.AjustarAsync(novo.Id, new AjusteUsuarioDto(UserStatus.Aprovado, null, null, null), Guid.NewGuid());

        var (resultado, user) = await svc.AutenticarAsync(new CredenciaisDto("a@b.com", "senha12345"));
        Assert.Equal(ResultadoLogin.Ok, resultado);
        Assert.NotNull(user);
    }

    [Fact]
    public async Task senha_errada_nao_entra_nem_aprovado()
    {
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("a@b.com", "senha12345", null));
        await svc.AjustarAsync(novo.Id, new AjusteUsuarioDto(UserStatus.Aprovado, null, null, null), Guid.NewGuid());

        var (resultado, _) = await svc.AutenticarAsync(new CredenciaisDto("a@b.com", "outrasenha"));
        Assert.Equal(ResultadoLogin.CredencialInvalida, resultado);
    }

    [Fact]
    public async Task bloqueado_tem_motivo_proprio()
    {
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("a@b.com", "senha12345", null));
        await svc.AjustarAsync(novo.Id, new AjusteUsuarioDto(UserStatus.Bloqueado, null, null, null), Guid.NewGuid());

        var (resultado, _) = await svc.AutenticarAsync(new CredenciaisDto("a@b.com", "senha12345"));
        // Não é 401 genérico: quem está bloqueado precisa falar com o admin, não
        // reconferir a senha.
        Assert.Equal(ResultadoLogin.Bloqueado, resultado);
    }

    [Fact]
    public async Task email_normaliza_maiuscula_e_espaco()
    {
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("  Peter@X.com ", "senha12345", null));
        Assert.Equal("peter@x.com", novo.Email);

        // E a segunda tentativa com outra grafia é conflito, não conta nova.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CadastrarAsync(new NovoUsuarioDto("PETER@x.com", "senha12345", null)));
    }

    [Fact]
    public async Task o_hash_nao_guarda_a_senha()
    {
        var (svc, repo) = Montar();
        await svc.CadastrarAsync(new NovoUsuarioDto("a@b.com", "senha12345", null));
        var user = await repo.ByEmailAsync("a@b.com");
        Assert.NotNull(user!.PasswordHash);
        Assert.DoesNotContain("senha12345", user.PasswordHash);
    }

    [Fact]
    public async Task senha_curta_e_recusada()
    {
        var (svc, _) = Montar();
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.CadastrarAsync(new NovoUsuarioDto("a@b.com", "1234567", null)));
    }

    [Fact]
    public async Task admin_nao_se_bloqueia_nem_se_remove()
    {
        var (svc, _) = Montar();
        var admin = await svc.CadastrarAsync(new NovoUsuarioDto("admin@x.com", "senha12345", null, Admin: true));

        // Um admin que se tranca deixa a base sem ninguém para aprovar ninguém.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AjustarAsync(admin.Id, new AjusteUsuarioDto(UserStatus.Bloqueado, null, null, null), admin.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AjustarAsync(admin.Id, new AjusteUsuarioDto(null, UserRoles.User, null, null), admin.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RemoverAsync(admin.Id, admin.Id));
    }

    [Fact]
    public async Task admin_criado_pelo_painel_ja_entra()
    {
        var (svc, _) = Montar();
        var admin = await svc.CadastrarAsync(new NovoUsuarioDto("admin@x.com", "senha12345", null, Admin: true));
        Assert.Equal(UserStatus.Aprovado, admin.Situacao);
        Assert.Equal(UserRoles.Admin, admin.Papel);

        var (resultado, _) = await svc.AutenticarAsync(new CredenciaisDto("admin@x.com", "senha12345"));
        Assert.Equal(ResultadoLogin.Ok, resultado);
    }

    [Fact]
    public async Task admin_inicial_e_idempotente()
    {
        var (svc, repo) = Montar();
        await svc.GarantirAdminInicialAsync("admin@x.com", "senha12345");
        await svc.GarantirAdminInicialAsync("admin@x.com", "outrasenha");
        Assert.Equal(1, await repo.CountAsync());

        // A segunda chamada não pode ter trocado a senha de quem já existia.
        var (resultado, _) = await svc.AutenticarAsync(new CredenciaisDto("admin@x.com", "senha12345"));
        Assert.Equal(ResultadoLogin.Ok, resultado);
    }
}
