using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TheDecrypter.Application.Auth;
using TheDecrypter.Application.Cnpj;
using TheDecrypter.Application.Lookups;
using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationDependencyInjection(this IServiceCollection services)
    {
        services.AddScoped<ICnpjService, CnpjService>();
        services.AddScoped<ILookupService, LookupService>();
        // PBKDF2 com os parâmetros padrão do ASP.NET Core; a versão do formato
        // fica gravada no próprio hash, então subir de versão depois é possível
        // sem invalidar as senhas (ver o `SuccessRehashNeeded` no AuthService).
        services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
