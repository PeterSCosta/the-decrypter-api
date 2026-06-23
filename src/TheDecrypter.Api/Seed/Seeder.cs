using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Ef;

namespace TheDecrypter.Api.Seed;

/// <summary>
/// Popula o Postgres com os datasets do front (the-decrypter/public/data):
/// municipios.json, streets.json, ceps.json.
///
/// Extensões + tabelas + índices: criados pelo db/schema.sql (initdb na primeira
/// subida do volume, e reaplicado pelo sidecar via `psql` em todo deploy —
/// schema.sql é 100% idempotente). Por isso NÃO criamos extensões aqui — o app
/// role não é superuser e CREATE EXTENSION falharia.
///
/// Idempotência self-healing: usa tabela `seed_state` para marcar progresso por
/// tabela. Se a execução anterior parou no meio (OOM, restart), a próxima
/// detecta `in_progress` e refaz com TRUNCATE — evita silenciosamente servir
/// dados parciais (a guarda antiga via AnyAsync mascarava esse failure mode).
/// </summary>
public static class Seeder
{
    public static async Task RunAsync(IServiceProvider sp, string dataDir, ILogger log)
    {
        var db = sp.GetRequiredService<DecrypterDbContext>();
        // seed_state vive em db/schema.sql (aplicado pelo initdb + sidecar psql).

        await SeedTable(db, log, "municipio", Path.Combine(dataDir, "municipios.json"), SeedMunicipios);
        await SeedTable(db, log, "street", Path.Combine(dataDir, "streets.json"), SeedStreets);
        await SeedTable(db, log, "cep", Path.Combine(dataDir, "ceps.json"), SeedCeps);

        log.LogInformation("Seed concluído.");
    }

    /// <summary>
    /// Roda <paramref name="loader"/> com self-healing:
    /// - status='complete' → pula;
    /// - status='in_progress' → TRUNCATE e refaz (execução anterior caiu);
    /// - ausente → marca in_progress, carrega, marca complete com row count.
    /// </summary>
    // Allow-list: TRUNCATE não aceita parâmetro para identificador, então o nome
    // entra direto no SQL. Validar contra esta lista bloqueia injeção via parâmetro.
    private static readonly HashSet<string> AllowedTables = ["municipio", "street", "cep"];

    private static async Task SeedTable(
        DecrypterDbContext db, ILogger log, string tableName, string path,
        Func<DecrypterDbContext, ILogger, string, Task<int>> loader)
    {
        if (!AllowedTables.Contains(tableName))
            throw new ArgumentException($"tabela não permitida no seed: {tableName}", nameof(tableName));
        if (!File.Exists(path)) { log.LogWarning("não achei {path}, pulando {table}", path, tableName); return; }

        var status = await db.Database
            .SqlQueryRaw<string>("SELECT status FROM seed_state WHERE table_name = {0}", tableName)
            .FirstOrDefaultAsync();

        if (status == "complete")
        {
            log.LogInformation("{table}: já populado (seed_state=complete), pulando", tableName);
            return;
        }

        // Upgrade path: DB pré-existente sem seed_state mas com linhas (ex.: dump
        // promovido, ou migração rolando num PG diferente do volume dedicado).
        // Sem isto o seeder PK-colidiria no INSERT.
        if (status is null)
        {
            // tableName validado via AllowedTables; seguro concatenar.
#pragma warning disable EF1002, EF1003
            var existing = await db.Database
                .SqlQueryRaw<long>("SELECT COUNT(*)::bigint AS \"Value\" FROM " + tableName)
                .FirstAsync();
#pragma warning restore EF1002, EF1003
            if (existing > 0)
            {
                log.LogInformation(
                    "{table}: {n} linhas pré-existentes sem seed_state, marcando como complete",
                    tableName, existing);
                await db.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO seed_state (table_name, status, rows_loaded, finished_at)
                    VALUES ({0}, 'complete', {1}, now());", tableName, (int)existing);
                return;
            }
        }

        if (status == "in_progress")
        {
            log.LogWarning("{table}: execução anterior incompleta, TRUNCATE + refazer", tableName);
            // CASCADE: futuro-proof se alguém adicionar FK (ex.: cep.municipio_ibge).
#pragma warning disable EF1002, EF1003
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE " + tableName + " RESTART IDENTITY CASCADE;");
#pragma warning restore EF1002, EF1003
        }

        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO seed_state (table_name, status) VALUES ({0}, 'in_progress')
            ON CONFLICT (table_name) DO UPDATE SET status='in_progress', rows_loaded=NULL, finished_at=NULL;",
            tableName);

        var loaded = await loader(db, log, path);

        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE seed_state SET status='complete', rows_loaded={1}, finished_at=now()
            WHERE table_name={0};", tableName, loaded);
        log.LogInformation("{table}: {n} inseridos (seed_state=complete)", tableName, loaded);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static string? Str(JsonElement o, string p) =>
        o.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement o, string p) =>
        o.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static double? Dbl(JsonElement o, string p) =>
        o.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static async Task<int> SeedMunicipios(DecrypterDbContext db, ILogger log, string path)
    {
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
        return list.Count;
    }

    private static async Task<int> SeedStreets(DecrypterDbContext db, ILogger log, string path)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var chunk = new List<Street>(5000);
        var total = 0;
        foreach (var r in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            chunk.Add(new Street
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
            if (chunk.Count >= 5000)
            {
                db.Streets.AddRange(chunk);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                total += chunk.Count;
                chunk.Clear();
            }
        }
        if (chunk.Count > 0)
        {
            db.Streets.AddRange(chunk);
            await db.SaveChangesAsync();
            total += chunk.Count;
        }
        return total;
    }

    private static async Task<int> SeedCeps(DecrypterDbContext db, ILogger log, string path)
    {
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
        return total;
    }
}
