namespace TheDecrypter.Domain.Gateways;

/// <summary>
/// O que a tela de login manda: um identificador e a senha.
///
/// ── POR QUE DUAS CHAVES PARA O MESMO CAMPO ──────────────────────────────────
/// `Identificador` é o nome novo; `Email` continua aceito porque é o que o
/// bundle ANTIGO manda. Um app aberto numa aba desde antes do deploy continua
/// entrando — trocar a chave no fio trancaria essas pessoas para fora até
/// recarregarem a página, sem nenhum aviso que explicasse o porquê.
/// </summary>
public record CredenciaisDto(string Senha, string? Identificador = null, string? Email = null)
{
    /// <summary>O que a pessoa digitou no campo único, venha de onde vier.</summary>
    public string Quem => (Identificador ?? Email ?? string.Empty).Trim();
}

/// <summary>
/// Conta nova. `Apelido` é o identificador de quem se cadastra hoje; `Email`
/// ficou opcional — mas a conta precisa de pelo menos um dos dois, senão
/// ninguém consegue entrar nela nunca mais.
/// </summary>
public record NovoUsuarioDto(
    string Senha,
    string? Apelido = null,
    string? Email = null,
    string? Nome = null,
    bool Admin = false,
    /// <summary>
    /// Nasce liberada, sem passar pela fila de aprovação.
    ///
    /// SEPARADO de `Admin` de propósito: antes, "já aprovado" era efeito
    /// colateral de virar administrador, e por isso a conta que o admin criava
    /// pelo painel — que é `admin: false` — nascia PENDENTE. O admin mandava a
    /// senha para a pessoa e ela não conseguia entrar.
    ///
    /// Como os dois campos são privilégio, o cadastro público força os DOIS
    /// como falsos: quem se cadastra sozinho não se aprova nem se promove.
    /// </summary>
    bool Aprovado = false);

/// <summary>Usuário como o app o vê. Nunca carrega o hash da senha.</summary>
public record UsuarioDto(
    Guid Id,
    string? Apelido,
    string? Email,
    string? Nome,
    string Papel,
    string Situacao,
    DateTimeOffset CriadoEm,
    DateTimeOffset? AprovadoEm);

public record SessaoDto(string Token, DateTimeOffset Expira, UsuarioDto Usuario);

/// <summary>Alteração feita pelo admin: qualquer campo ausente fica como está.</summary>
public record AjusteUsuarioDto(
    string? Situacao,
    string? Papel,
    string? Senha,
    string? Nome,
    /// <summary>Definir o apelido de uma conta antiga, ou corrigir um errado.</summary>
    string? Apelido = null);

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
