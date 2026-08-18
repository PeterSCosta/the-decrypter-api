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

        // Pool de DbContext (menos alocação sob carga) + sem tracking por padrão
        // (a API é read-mostly; o seeder usa Add/SaveChanges normalmente).
        services.AddDbContextPool<DecrypterDbContext>(options =>
            options.UseNpgsql(conn).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddScoped<ICepRepository, CepRepository>();
        services.AddScoped<IStreetRepository, StreetRepository>();
        services.AddScoped<IMunicipioRepository, MunicipioRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPosteRepository, PosteRepository>();
        services.AddScoped<IAirportRepository, AirportRepository>();
        services.AddScoped<ICidRepository, CidRepository>();
        services.AddScoped<ILoteBlumenauRepository, LoteBlumenauRepository>();
        return services;
    }
}
