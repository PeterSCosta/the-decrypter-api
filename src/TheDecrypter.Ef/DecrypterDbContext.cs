using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Ef;

public class DecrypterDbContext(DbContextOptions<DecrypterDbContext> options) : DbContext(options)
{
    public DbSet<Cep> Ceps => Set<Cep>();
    public DbSet<Street> Streets => Set<Street>();
    public DbSet<Municipio> Municipios => Set<Municipio>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Poste> Postes => Set<Poste>();
    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<StreetRol> StreetRol => Set<StreetRol>();
    public DbSet<Bridge> Bridges => Set<Bridge>();
    public DbSet<Cid> Cids => Set<Cid>();
    public DbSet<LoteBlumenau> LotesBlumenau => Set<LoteBlumenau>();

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

        b.Entity<StreetRol>(e =>
        {
            e.ToTable("street_rol");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Codigo).HasColumnName("codigo");
            e.Property(x => x.Tipo).HasColumnName("tipo");
            e.Property(x => x.Nome).HasColumnName("nome");
            e.Property(x => x.BairroNum).HasColumnName("bairro_num");
            e.Property(x => x.Bairro).HasColumnName("bairro");
            e.Property(x => x.NumLei).HasColumnName("num_lei");
            e.Property(x => x.DataLei).HasColumnName("data_lei");
            e.Property(x => x.Localizacao).HasColumnName("localizacao");
            e.Property(x => x.Ext).HasColumnName("ext");
            e.Property(x => x.Larg).HasColumnName("larg");
            e.Property(x => x.Atas).HasColumnName("atas");
            e.Property(x => x.Lat).HasColumnName("lat");
            e.Property(x => x.Lng).HasColumnName("lng");
        });

        b.Entity<Bridge>(e =>
        {
            e.ToTable("bridge");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Nome).HasColumnName("nome");
            e.Property(x => x.NomeOsm).HasColumnName("nome_osm");
            e.Property(x => x.Apelidos).HasColumnName("apelidos");
            e.Property(x => x.Tipo).HasColumnName("tipo");
            e.Property(x => x.Fonte).HasColumnName("fonte");
            e.Property(x => x.Lei).HasColumnName("lei");
            e.Property(x => x.NumLei).HasColumnName("num_lei");
            e.Property(x => x.AnoLei).HasColumnName("ano_lei");
            e.Property(x => x.DataLei).HasColumnName("data_lei");
            e.Property(x => x.Ementa).HasColumnName("ementa");
            e.Property(x => x.UrlLei).HasColumnName("url_lei");
            e.Property(x => x.Situacao).HasColumnName("situacao");
            e.Property(x => x.Lat).HasColumnName("lat");
            e.Property(x => x.Lng).HasColumnName("lng");
            e.Property(x => x.Comprimento).HasColumnName("comprimento");
            e.Property(x => x.Via).HasColumnName("via");
            e.Property(x => x.Material).HasColumnName("material");
            e.Property(x => x.Transpoe).HasColumnName("transpoe");
            e.Property(x => x.Bairros).HasColumnName("bairros");
        });

        b.Entity<LoteBlumenau>(e =>
        {
            e.ToTable("lote_blumenau");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Inscricao).HasColumnName("inscricao").HasMaxLength(15);
            e.Property(x => x.Iq).HasColumnName("iq");
            e.Property(x => x.Logradouro).HasColumnName("logradouro");
            e.Property(x => x.Numero).HasColumnName("numero");
            e.Property(x => x.Bairro).HasColumnName("bairro");
            e.Property(x => x.Cep).HasColumnName("cep").HasMaxLength(8);
            e.Property(x => x.Lat).HasColumnName("lat");
            e.Property(x => x.Lng).HasColumnName("lng");
            e.Property(x => x.AreaM2).HasColumnName("area_m2");
            e.Property(x => x.Enderecos).HasColumnName("enderecos");
        });

        b.Entity<Cid>(e =>
        {
            e.ToTable("cid");
            e.HasKey(x => x.Codigo);
            e.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(4);
            e.Property(x => x.Descricao).HasColumnName("descricao");
            e.Property(x => x.Capitulo).HasColumnName("capitulo");
            e.Property(x => x.CapituloDesc).HasColumnName("capitulo_desc");
            e.Property(x => x.GrupoDesc).HasColumnName("grupo_desc");
            e.Property(x => x.Classif).HasColumnName("classif").HasMaxLength(1);
            e.Property(x => x.Sexo).HasColumnName("sexo").HasMaxLength(1);
            e.Property(x => x.NaoObito).HasColumnName("nao_obito");
        });

        b.Entity<Airport>(e =>
        {
            e.ToTable("airport");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Iata).HasColumnName("iata");
            e.Property(x => x.Icao).HasColumnName("icao");
            e.Property(x => x.Nome).HasColumnName("nome");
            e.Property(x => x.Cidade).HasColumnName("cidade");
            e.Property(x => x.Pais).HasColumnName("pais");
            e.Property(x => x.Lat).HasColumnName("lat");
            e.Property(x => x.Lng).HasColumnName("lng");
        });

        b.Entity<Poste>(e =>
        {
            e.ToTable("poste");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Plaqueta).HasColumnName("plaqueta");
            e.Property(x => x.Lat).HasColumnName("lat");
            e.Property(x => x.Lng).HasColumnName("lng");
            e.Property(x => x.Rua).HasColumnName("rua");
            e.Property(x => x.RuaTipo).HasColumnName("rua_tipo");
            e.Property(x => x.RuaNome).HasColumnName("rua_nome");
            e.Property(x => x.RuaId).HasColumnName("rua_id");
            e.Property(x => x.Numero).HasColumnName("numero");
            e.Property(x => x.Bairro).HasColumnName("bairro");
            e.Property(x => x.Estrutura).HasColumnName("estrutura");
            e.Property(x => x.EstruturaId).HasColumnName("estrutura_id");
            e.Property(x => x.Tipo).HasColumnName("tipo");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.PontosLuminosos).HasColumnName("pontos_luminosos");
            e.Property(x => x.Altura).HasColumnName("altura");
            e.Property(x => x.Instalacao).HasColumnName("instalacao");
            e.Property(x => x.Alteracao).HasColumnName("alteracao");
            e.Property(x => x.Cor).HasColumnName("cor");
            // `coord_bnu` fica FORA do modelo: é coluna gerada e o PG recusa
            // INSERT que a mencione. `DistanciaMetros` só existe nas consultas
            // por proximidade, preenchida pelo SELECT.
            e.Ignore(x => x.DistanciaMetros);
            e.HasIndex(x => x.Plaqueta);
        });

        b.Entity<AppUser>(e =>
        {
            e.ToTable("app_user");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.Nickname).HasColumnName("nickname");
            e.Property(x => x.DisplayName).HasColumnName("display_name");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ApprovedAt).HasColumnName("approved_at");
            e.Property(x => x.ApprovedBy).HasColumnName("approved_by");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Nickname).IsUnique();
            // `PodeEntrar`/`IsAdmin`/`Rotulo` são derivadas: existem só em C#.
            // Sem o `Ignore`, o EF procuraria uma coluna para cada uma e a
            // consulta quebraria na primeira leitura.
            e.Ignore(x => x.PodeEntrar);
            e.Ignore(x => x.IsAdmin);
            e.Ignore(x => x.Rotulo);
        });
    }
}
