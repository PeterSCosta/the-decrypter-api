using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TheDecrypter.Domain.Exceptions;
using TheDecrypter.Domain.Repositories;
using TheDecrypter.Ef.Repositories;

namespace TheDecrypter.Ef;

public static class EfDependencyInjection
{
    public static IServiceCollection AddEfDependencyInjection(
        this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("DecrypterDB")
            ?? throw new NotFoundException("DecrypterDB connection not found");

        services.AddDbContext<DecrypterDbContext>(options => options.UseNpgsql(conn));
        services.AddScoped<ICepRepository, CepRepository>();
        return services;
    }
}
