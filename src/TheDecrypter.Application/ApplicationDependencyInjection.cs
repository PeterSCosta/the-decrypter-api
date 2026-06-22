using Microsoft.Extensions.DependencyInjection;
using TheDecrypter.Application.Cnpj;

namespace TheDecrypter.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationDependencyInjection(this IServiceCollection services)
    {
        services.AddScoped<ICnpjService, CnpjService>();
        return services;
    }
}
