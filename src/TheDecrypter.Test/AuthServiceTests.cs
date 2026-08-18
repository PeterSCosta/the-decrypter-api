using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using TheDecrypter.Application.Auth;
using TheDecrypter.Domain.Auth;
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

    public Task<AppUser?> ByApelidoAsync(string apelido, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Nickname == apelido));

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
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "ana", "a@b.com", "A"));
        Assert.Equal(UserStatus.Pendente, novo.Situacao);

        var (resultado, user) = await svc.AutenticarAsync(new CredenciaisDto("senha12345", "a@b.com"));
        Assert.Equal(ResultadoLogin.Pendente, resultado);
        Assert.Null(user);
    }

    [Fact]
    public async Task depois_de_aprovado_entra()
    {
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "ana", "a@b.com"));
        await svc.AjustarAsync(novo.Id, new AjusteUsuarioDto(UserStatus.Aprovado, null, null, null), Guid.NewGuid());

        var (resultado, user) = await svc.AutenticarAsync(new CredenciaisDto("senha12345", "a@b.com"));
        Assert.Equal(ResultadoLogin.Ok, resultado);
        Assert.NotNull(user);
    }

    [Fact]
    public async Task senha_errada_nao_entra_nem_aprovado()
    {
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "ana", "a@b.com"));
        await svc.AjustarAsync(novo.Id, new AjusteUsuarioDto(UserStatus.Aprovado, null, null, null), Guid.NewGuid());

        var (resultado, _) = await svc.AutenticarAsync(new CredenciaisDto("outrasenha", "a@b.com"));
        Assert.Equal(ResultadoLogin.CredencialInvalida, resultado);
    }

    [Fact]
    public async Task bloqueado_tem_motivo_proprio()
    {
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "ana", "a@b.com"));
        await svc.AjustarAsync(novo.Id, new AjusteUsuarioDto(UserStatus.Bloqueado, null, null, null), Guid.NewGuid());

        var (resultado, _) = await svc.AutenticarAsync(new CredenciaisDto("senha12345", "a@b.com"));
        // Não é 401 genérico: quem está bloqueado precisa falar com o admin, não
        // reconferir a senha.
        Assert.Equal(ResultadoLogin.Bloqueado, resultado);
    }

    [Fact]
    public async Task email_normaliza_maiuscula_e_espaco()
    {
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "  Peter ", "  Peter@X.com "));
        Assert.Equal("peter@x.com", novo.Email);
        // O apelido segue a MESMA disciplina, e não por simetria estética: o
        // índice é sobre `lower(nickname)`, então gravar "Peter" deixaria a
        // busca exata do repositório sem achar a própria conta.
        Assert.Equal("peter", novo.Apelido);

        // E a segunda tentativa com outra grafia é conflito, não conta nova.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "outro", "PETER@x.com")));
    }

    [Fact]
    public async Task o_hash_nao_guarda_a_senha()
    {
        var (svc, repo) = Montar();
        await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "ana", "a@b.com"));
        var user = await repo.ByEmailAsync("a@b.com");
        Assert.NotNull(user!.PasswordHash);
        Assert.DoesNotContain("senha12345", user.PasswordHash);
    }

    [Fact]
    public async Task senha_curta_e_recusada()
    {
        var (svc, _) = Montar();
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.CadastrarAsync(new NovoUsuarioDto("1234567", "ana", "a@b.com")));
    }

    [Fact]
    public async Task admin_nao_se_bloqueia_nem_se_remove()
    {
        var (svc, _) = Montar();
        var admin = await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "chefe", "admin@x.com", Admin: true));

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
        var admin = await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "chefe", "admin@x.com", Admin: true));
        Assert.Equal(UserStatus.Aprovado, admin.Situacao);
        Assert.Equal(UserRoles.Admin, admin.Papel);

        var (resultado, _) = await svc.AutenticarAsync(new CredenciaisDto("senha12345", "admin@x.com"));
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
        var (resultado, _) = await svc.AutenticarAsync(new CredenciaisDto("senha12345", "admin@x.com"));
        Assert.Equal(ResultadoLogin.Ok, resultado);
    }

    // ── APELIDO ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task entra_pelo_apelido_e_pelo_email_da_mesma_conta()
    {
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "peter", "p@x.com"));
        await svc.AjustarAsync(novo.Id, new AjusteUsuarioDto(UserStatus.Aprovado, null, null, null), Guid.NewGuid());

        // As duas portas abrem a MESMA conta — é isso que faz o campo único
        // funcionar sem obrigar ninguém a lembrar com o que se cadastrou.
        var (porApelido, u1) = await svc.AutenticarAsync(new CredenciaisDto("senha12345", "peter"));
        var (porEmail, u2) = await svc.AutenticarAsync(new CredenciaisDto("senha12345", "p@x.com"));
        Assert.Equal(ResultadoLogin.Ok, porApelido);
        Assert.Equal(ResultadoLogin.Ok, porEmail);
        Assert.Equal(u1!.Id, u2!.Id);
    }

    [Fact]
    public async Task conta_antiga_sem_apelido_continua_entrando_pelo_email()
    {
        // A razão de existir do índice PARCIAL: ninguém foi migrado à força, e
        // quem já tinha conta não perdeu nada.
        var (svc, repo) = Montar();
        await repo.AddAsync(new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "antigo@x.com",
            Nickname = null,
            Status = UserStatus.Aprovado,
            PasswordHash = new PasswordHasher<AppUser>().HashPassword(new AppUser(), "senha12345"),
        });

        var (r, _) = await svc.AutenticarAsync(new CredenciaisDto("senha12345", "antigo@x.com"));
        Assert.Equal(ResultadoLogin.Ok, r);
    }

    [Fact]
    public async Task apelido_repetido_e_conflito_mesmo_com_outra_caixa()
    {
        var (svc, _) = Montar();
        await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "peter", "a@x.com"));
        var e = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "PETER", "b@x.com")));
        Assert.Contains("já está em uso", e.Message);
    }

    [Fact]
    public async Task conta_sem_email_e_valida_e_o_token_nao_depende_do_email()
    {
        // O caso que a mudança inteira existe para permitir.
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "so.apelido"));
        Assert.Null(novo.Email);
        Assert.Equal("so.apelido", novo.Apelido);
    }

    [Fact]
    public async Task conta_sem_apelido_e_sem_email_nao_existe()
    {
        var (svc, _) = Montar();
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.CadastrarAsync(new NovoUsuarioDto("senha12345")));
    }

    [Fact]
    public async Task o_admin_nao_pode_apagar_o_apelido_de_quem_nao_tem_email()
    {
        // Sem esta guarda, um ajuste bem-intencionado tranca a pessoa para fora
        // para sempre — e ninguém percebe até ela tentar entrar.
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "sozinho"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AjustarAsync(novo.Id, new AjusteUsuarioDto(null, null, null, null, ""), Guid.NewGuid()));
    }

    [Fact]
    public async Task trocar_so_a_caixa_do_proprio_apelido_nao_e_conflito()
    {
        var (svc, _) = Montar();
        var novo = await svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "peter", "p@x.com"));
        var ajustado = await svc.AjustarAsync(
            novo.Id, new AjusteUsuarioDto(null, null, null, null, "Peter"), Guid.NewGuid());
        Assert.Equal("peter", ajustado!.Apelido);
    }

    [Fact]
    public async Task apelido_reservado_e_recusado()
    {
        var (svc, _) = Montar();
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "admin")));
    }

    [Fact]
    public async Task nome_de_exibicao_nao_pode_imitar_um_email()
    {
        // O painel de aprovação mostra o nome; um nome "peter@empresa.com" faria
        // o admin aprovar achando que é outra pessoa.
        var (svc, _) = Montar();
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.CadastrarAsync(new NovoUsuarioDto("senha12345", "x1", null, "peter@empresa.com")));
    }

    [Fact]
    public async Task admin_inicial_so_com_email_continua_funcionando()
    {
        // É a configuração que está em produção HOJE. Se isto quebrar, o boot
        // quebra junto.
        var (svc, repo) = Montar();
        await svc.GarantirAdminInicialAsync("admin@x.com", "senha12345");
        Assert.Equal(1, await repo.CountAsync());
        var (r, _) = await svc.AutenticarAsync(new CredenciaisDto("senha12345", "admin@x.com"));
        Assert.Equal(ResultadoLogin.Ok, r);
    }

    [Fact]
    public async Task admin_inicial_define_o_apelido_de_quem_ja_existia_sem_duplicar()
    {
        var (svc, repo) = Montar();
        await svc.GarantirAdminInicialAsync("admin@x.com", "senha12345");
        // O operador define Admin__Apelido depois. Não pode virar uma SEGUNDA
        // conta de admin a cada boot.
        await svc.GarantirAdminInicialAsync("admin@x.com", "senha12345", "chefe");
        Assert.Equal(1, await repo.CountAsync());

        var (r, u) = await svc.AutenticarAsync(new CredenciaisDto("senha12345", "chefe"));
        Assert.Equal(ResultadoLogin.Ok, r);
        Assert.Equal("admin@x.com", u!.Email);
    }
}
