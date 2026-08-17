using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using TheDecrypter.Ef;

namespace TheDecrypter.Api.Controllers;

/// <summary>Uma base do acervo, como a Biblioteca a apresenta.</summary>
public record BaseDoAcervo(
    string Id,
    string Nome,
    string Indexa,
    string Origem,
    long Registros,
    bool Navegavel);

/// <summary>
/// O acervo: o que a bancada sabe, e quantos registros de cada coisa.
///
/// As contagens vêm do banco, não de constante no código. Números chumbados
/// envelhecem em silêncio — e uma biblioteca que mente sobre o próprio tamanho
/// é pior que não ter biblioteca.
/// </summary>
[ApiController]
[Route("api/library")]
[Authorize]
[OutputCache(PolicyName = "lookups")]
public class LibraryController(DecrypterDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Catalogo(CancellationToken ct)
    {
        // Sequencial: as quatro contagens dividem o mesmo DbContext.
        var bases = new List<BaseDoAcervo>
        {
            new("poste", "Postes de iluminação", "plaqueta → coordenada, rua, bairro, luminária",
                "Cidade Iluminada (Exati/IPBL) — coleta própria", await db.Postes.CountAsync(ct), true),
            new("cep", "CEPs de Santa Catarina", "CEP → logradouro, bairro, município, coordenada",
                "Base dos Dados", await db.Ceps.CountAsync(ct), true),
            new("street", "Rol de Ruas de Blumenau", "código / nº da lei / nome → rua, bairro",
                "Prefeitura de Blumenau", await db.Streets.CountAsync(ct), true),
            new("municipio", "Municípios do Brasil", "código IBGE → nome, UF",
                "IBGE", await db.Municipios.CountAsync(ct), true),
            new("airport", "Aeroportos do mundo", "IATA / ICAO → nome, cidade, país, coordenada",
                "OpenFlights", await db.Airports.CountAsync(ct), true),
        };
        return Ok(new { total = bases.Count, hits = bases });
    }

    /// <summary>Navega uma base, paginada e filtrável.</summary>
    [HttpGet("{baseId}")]
    public async Task<IActionResult> Navegar(
        string baseId,
        [FromQuery] string? q,
        [FromQuery] int page = 0,
        [FromQuery] int size = 50,
        CancellationToken ct = default)
    {
        var n = Math.Clamp(size, 1, 200);
        var salto = Math.Max(0, page) * n;
        var termo = (q ?? string.Empty).Trim();
        var like = $"%{termo}%";

        switch (baseId)
        {
            case "poste":
            {
                var query = db.Postes.AsQueryable();
                if (termo.Length > 0)
                {
                    query = termo.All(char.IsDigit)
                        ? query.Where(p => p.Plaqueta != null && EF.Functions.Like(p.Plaqueta, $"{termo}%"))
                        : query.Where(p => EF.Functions.ILike(p.Rua!, like) || EF.Functions.ILike(p.Bairro!, like));
                }
                var total = await query.CountAsync(ct);
                var hits = await query.OrderBy(p => p.Id).Skip(salto).Take(n).ToListAsync(ct);
                return Ok(new { total, page, size = n, hits });
            }
            case "cep":
            {
                var query = db.Ceps.AsQueryable();
                if (termo.Length > 0)
                    query = query.Where(c =>
                        EF.Functions.Like(c.Code, $"{termo}%") || EF.Functions.ILike(c.Logradouro!, like));
                var total = await query.CountAsync(ct);
                var hits = await query.OrderBy(c => c.Code).Skip(salto).Take(n).ToListAsync(ct);
                return Ok(new { total, page, size = n, hits });
            }
            case "street":
            {
                var query = db.Streets.AsQueryable();
                if (termo.Length > 0) query = query.Where(s => EF.Functions.ILike(s.Nome, like));
                var total = await query.CountAsync(ct);
                var hits = await query.OrderBy(s => s.Nome).Skip(salto).Take(n).ToListAsync(ct);
                return Ok(new { total, page, size = n, hits });
            }
            case "municipio":
            {
                var query = db.Municipios.AsQueryable();
                if (termo.Length > 0) query = query.Where(m => EF.Functions.ILike(m.Nome, like));
                var total = await query.CountAsync(ct);
                var hits = await query.OrderBy(m => m.Nome).Skip(salto).Take(n).ToListAsync(ct);
                return Ok(new { total, page, size = n, hits });
            }
            case "airport":
            {
                var query = db.Airports.AsQueryable();
                if (termo.Length > 0)
                    query = query.Where(a =>
                        a.Iata == termo.ToUpper() || a.Icao == termo.ToUpper() ||
                        EF.Functions.ILike(a.Nome!, like) || EF.Functions.ILike(a.Cidade!, like));
                var total = await query.CountAsync(ct);
                var hits = await query.OrderBy(a => a.Iata).Skip(salto).Take(n).ToListAsync(ct);
                return Ok(new { total, page, size = n, hits });
            }
            default:
                return NotFound(new { message = "base não encontrada" });
        }
    }
}
