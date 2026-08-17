namespace TheDecrypter.Domain.Gateways;

public record CredenciaisDto(string Email, string Senha);

public record NovoUsuarioDto(string Email, string Senha, string? Nome, bool Admin = false);

/// <summary>Usuário como o app o vê. Nunca carrega o hash da senha.</summary>
public record UsuarioDto(
    Guid Id,
    string Email,
    string? Nome,
    string Papel,
    string Situacao,
    DateTimeOffset CriadoEm,
    DateTimeOffset? AprovadoEm);

public record SessaoDto(string Token, DateTimeOffset Expira, UsuarioDto Usuario);

/// <summary>Alteração feita pelo admin: qualquer campo ausente fica como está.</summary>
public record AjusteUsuarioDto(string? Situacao, string? Papel, string? Senha, string? Nome);

/// <summary>
/// Resultado de uma tentativa de login. `Motivo` existe para o app distinguir
/// "senha errada" de "ainda não aprovado" — dois problemas com soluções bem
/// diferentes para quem está do outro lado.
/// </summary>
public enum ResultadoLogin
{
    Ok,
    CredencialInvalida,
    Pendente,
    Bloqueado,
}
