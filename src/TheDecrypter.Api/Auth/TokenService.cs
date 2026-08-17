using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Api.Auth;

/// <summary>Configuração do JWT, lida de `Jwt:*` (env `Jwt__*` em produção).</summary>
public class JwtOptions
{
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "the-decrypter-api";
    public string Audience { get; set; } = "the-decrypter";
    /// <summary>Curto de propósito: o token vive em `localStorage`, sem refresh.</summary>
    public int HorasDeValidade { get; set; } = 12;

    /// <summary>
    /// HS256 exige chave de pelo menos 256 bits. Com menos, o
    /// `SigningCredentials` estoura **na primeira tentativa de login** — não no
    /// boot. Isso já derrubou o login inteiro de um projeto irmão em produção,
    /// com a API "saudável" e ninguém entendendo o porquê. Aqui a validação roda
    /// no startup e a API se recusa a subir.
    /// </summary>
    public const int MinimoDeBytes = 32;
}

public interface ITokenService
{
    (string Token, DateTimeOffset Expira) Emitir(AppUser user);
}

public class TokenService(JwtOptions options) : ITokenService
{
    private readonly SymmetricSecurityKey _key =
        new(Encoding.UTF8.GetBytes(options.SigningKey));

    public (string, DateTimeOffset) Emitir(AppUser user)
    {
        var expira = DateTimeOffset.UtcNow.AddHours(options.HorasDeValidade);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, user.Role),
        };
        if (user.DisplayName is { } nome) claims.Add(new Claim(ClaimTypes.Name, nome));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: expira.UtcDateTime,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
