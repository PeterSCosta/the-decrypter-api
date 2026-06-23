using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Ef;

namespace TheDecrypter.Api.Seed;

/// <summary>
/// Popula o Postgres com os datasets do front (the-decrypter/public/data):
/// municipios.json, streets.json, ceps.json. Idempotente por tabela (pula se já
/// houver dados). Rode: `dotnet run --project src/TheDecrypter.Api -- seed --data &lt;dir&gt;`.
/// </summary>
public static class Seeder
{
    public static async Task RunAsync(IServiceProvider sp, string dataDir, ILogger log)
    {
        var db = sp.GetRequiredService<DecrypterDbContext>();
        await db.Database.EnsureCreatedAsync();

        await SeedMunicipios(db, log, Path.Combine(dataDir, "municipios.json"));
        await SeedStreets(db, log, Path.Combine(dataDir, "streets.json"));
        await SeedCeps(db, log, Path.Combine(dataDir, "ceps.json"));
        log.LogInformation("Seed concluído.");
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static string? Str(JsonElement o, string p) =>
        o.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement o, string p) =>
        o.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static double? Dbl(JsonElement o, string p) =>
        o.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static async Task SeedMunicipios(DecrypterDbContext db, ILogger log, string path)
    {
        if (!File.Exists(path)) { log.LogWarning("não achei {path}", path); return; }
        if (await db.Municipios.AnyAsync()) { log.LogInformation("municipios já populado, pulando"); return; }

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var list = new List<Municipio>();
        foreach (var r in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            list.Add(new Municipio
            {
                CodigoIbge = int.Parse(r[0].GetString()!),
                Nome = r[1].GetString()!,
                Uf = r[2].GetString()!,
            });
        }
        db.Municipios.AddRange(list);
        await db.SaveChangesAsync();
        log.LogInformation("municipios: {n} inseridos", list.Count);
    }

    private static async Task SeedStreets(DecrypterDbContext db, ILogger log, string path)
    {
        if (!File.Exists(path)) { log.LogWarning("não achei {path}", path); return; }
        if (await db.Streets.AnyAsync()) { log.LogInformation("street já populado, pulando"); return; }

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var list = new List<Street>();
        foreach (var r in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            list.Add(new Street
            {
                Codigo = r.GetProperty("codigo").GetInt32(),
                Tipo = Str(r, "tipo") ?? "",
                Nome = Str(r, "nome") ?? "",
                Bairro = NullIfEmpty(Str(r, "bairro")),
                NumLei = Int(r, "numLei"),
                DataLei = NullIfEmpty(Str(r, "dataLei")),
                Localizacao = NullIfEmpty(Str(r, "localizacao")),
                Ext = Int(r, "ext"),
                Larg = Dbl(r, "larg"),
            });
        }
        db.Streets.AddRange(list);
        await db.SaveChangesAsync();
        log.LogInformation("street: {n} inseridos", list.Count);
    }

    private static async Task SeedCeps(DecrypterDbContext db, ILogger log, string path)
    {
        if (!File.Exists(path)) { log.LogWarning("não achei {path}", path); return; }
        if (await db.Ceps.AnyAsync()) { log.LogInformation("cep já populado, pulando"); return; }

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = doc.RootElement;
        var municipios = root.GetProperty("municipios").EnumerateArray()
            .Select(x => x.GetString() ?? "").ToArray();

        var chunk = new List<Cep>(5000);
        var total = 0;
        foreach (var r in root.GetProperty("rows").EnumerateArray())
        {
            var idx = r[3].GetInt32();
            chunk.Add(new Cep
            {
                Code = r[0].GetString()!,
                Logradouro = NullIfEmpty(r[1].GetString()),
                Bairro = NullIfEmpty(r[2].GetString()),
                Localidade = idx >= 0 && idx < municipios.Length ? municipios[idx] : null,
                Uf = "SC",
                Lat = r[4].ValueKind == JsonValueKind.Number ? r[4].GetDouble() : null,
                Lng = r[5].ValueKind == JsonValueKind.Number ? r[5].GetDouble() : null,
            });
            if (chunk.Count >= 5000)
            {
                db.Ceps.AddRange(chunk);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                total += chunk.Count;
                chunk.Clear();
            }
        }
        if (chunk.Count > 0)
        {
            db.Ceps.AddRange(chunk);
            await db.SaveChangesAsync();
            total += chunk.Count;
        }
        log.LogInformation("cep: {n} inseridos", total);
    }
}
