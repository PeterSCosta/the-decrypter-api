using Microsoft.Extensions.DependencyInjection;
using TheDecrypter.Application.Cnpj;
using TheDecrypter.Application.Lookups;

namespace TheDecrypter.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationDependencyInjection(this IServiceCollection services)
    {
        services.AddScoped<ICnpjService, CnpjService>();
        services.AddScoped<ILookupService, LookupService>();
        return services;
    }
}
