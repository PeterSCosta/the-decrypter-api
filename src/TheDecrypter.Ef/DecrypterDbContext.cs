using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Ef;

public class DecrypterDbContext(DbContextOptions<DecrypterDbContext> options) : DbContext(options)
{
    public DbSet<Cep> Ceps => Set<Cep>();
    public DbSet<Street> Streets => Set<Street>();
    public DbSet<Municipio> Municipios => Set<Municipio>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Cep>(e =>
        {
            e.ToTable("cep");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasColumnName("code").HasMaxLength(8);
            e.Property(x => x.Logradouro).HasColumnName("logradouro");
            e.Property(x => x.Bairro).HasColumnName("bairro");
            e.Property(x => x.Localidade).HasColumnName("localidade");
            e.Property(x => x.MunicipioIbge).HasColumnName("municipio_ibge");
            e.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2);
            e.Property(x => x.Lat).HasColumnName("lat");
            e.Property(x => x.Lng).HasColumnName("lng");
            e.HasIndex(x => x.Uf);
        });

        b.Entity<Street>(e =>
        {
            e.ToTable("street");
            e.HasKey(x => x.Codigo);
            e.Property(x => x.Codigo).HasColumnName("codigo");
            e.Property(x => x.Tipo).HasColumnName("tipo");
            e.Property(x => x.Nome).HasColumnName("nome");
            e.Property(x => x.Bairro).HasColumnName("bairro");
            e.Property(x => x.NumLei).HasColumnName("num_lei");
            e.Property(x => x.DataLei).HasColumnName("data_lei");
            e.Property(x => x.Localizacao).HasColumnName("localizacao");
            e.Property(x => x.Ext).HasColumnName("ext");
            e.Property(x => x.Larg).HasColumnName("larg");
            e.HasIndex(x => x.NumLei);
        });

        b.Entity<Municipio>(e =>
        {
            e.ToTable("municipio");
            e.HasKey(x => x.CodigoIbge);
            e.Property(x => x.CodigoIbge).HasColumnName("codigo_ibge").ValueGeneratedNever();
            e.Property(x => x.Nome).HasColumnName("nome");
            e.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2);
            e.HasIndex(x => x.Uf);
        });

        b.Entity<AppUser>(e =>
        {
            e.ToTable("app_user");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.DisplayName).HasColumnName("display_name");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Email).IsUnique();
        });
    }
}
