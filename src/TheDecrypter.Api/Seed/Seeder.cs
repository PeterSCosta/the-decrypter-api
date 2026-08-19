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
        await SeedTable(db, log, "poste", Path.Combine(dataDir, "postes.json"), SeedPostes);
        await SeedTable(db, log, "airport", Path.Combine(dataDir, "airports.json"), SeedAirports);
        await SeedTable(db, log, "street_rol", Path.Combine(dataDir, "streets.json"), SeedStreetRol);
        await SeedTable(db, log, "bridge", Path.Combine(dataDir, "bridges.json"), SeedBridges);
        await SeedTable(db, log, "cid", Path.Combine(dataDir, "cid10.json"), SeedCid);
        await SeedTable(
            db, log, "lote_blumenau", Path.Combine(dataDir, "lotes-blumenau.json"), SeedLotes);

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
    private static readonly HashSet<string> AllowedTables =
        ["municipio", "street", "cep", "poste", "airport", "street_rol", "bridge", "cid", "lote_blumenau"];

    private static async Task SeedTable(
        DecrypterDbContext db, ILogger log, string tableName, string path,
        Func<DecrypterDbContext, ILogger, string, Task<int>> loader)
    {
        if (!AllowedTables.Contains(tableName))
            throw new ArgumentException($"tabela não permitida no seed: {tableName}", nameof(tableName));
        if (!File.Exists(path)) { log.LogWarning("não achei {path}, pulando {table}", path, tableName); return; }

        // Impressão digital do ARQUIVO-FONTE. Sem ela, `status='complete'` era
        // definitivo: a tabela nunca mais recarregava, e um dataset regenerado
        // NUNCA chegava em produção. O deploy passava verde, a coluna nova subia
        // vazia e nada avisava — o pior tipo de falha, a silenciosa.
        //
        // Descoberto ao acrescentar o endereço aos 84.539 lotes: 57.273 lotes
        // ganharam número de porta no arquivo, e o banco de produção continuaria
        // com o arquivo antigo até alguém lembrar de um TRUNCATE manual.
        var fingerprint = await ArquivoFingerprint(path);

        // SqlQueryRaw<T> exige que a coluna escalar se chame "Value" (convenção do EF).
        var status = await db.Database
            .SqlQueryRaw<string>("SELECT status AS \"Value\" FROM seed_state WHERE table_name = {0}", tableName)
            .FirstOrDefaultAsync();

        if (status == "complete")
        {
            var gravado = await db.Database
                .SqlQueryRaw<string?>(
                    "SELECT source_hash AS \"Value\" FROM seed_state WHERE table_name = {0}", tableName)
                .FirstOrDefaultAsync();

            if (gravado == fingerprint)
            {
                log.LogInformation("{table}: já populado e o arquivo não mudou, pulando", tableName);
                return;
            }

            // Banco antigo, semeado antes de existir impressão digital: não dá
            // para saber se o arquivo é o mesmo. Assumir que É seria arriscar
            // recarregar 84 mil linhas a cada deploy; assumir que NÃO seria
            // manter o bug. O meio-termo honesto é adotar o hash atual e avisar.
            if (gravado is null)
            {
                log.LogInformation(
                    "{table}: sem impressão digital gravada (semeado antes desta versão) — adotando a atual sem recarregar",
                    tableName);
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE seed_state SET source_hash={1} WHERE table_name={0};", tableName, fingerprint);
                return;
            }

            log.LogWarning(
                "{table}: o arquivo MUDOU ({velho} → {novo}), TRUNCATE + recarregar",
                tableName, gravado[..8], fingerprint[..8]);
#pragma warning disable EF1002, EF1003
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE " + tableName + " RESTART IDENTITY CASCADE;");
#pragma warning restore EF1002, EF1003
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
            UPDATE seed_state SET status='complete', rows_loaded={1}, finished_at=now(), source_hash={2}
            WHERE table_name={0};", tableName, loaded, fingerprint);
        log.LogInformation("{table}: {n} inseridos (seed_state=complete)", tableName, loaded);
    }

    /// <summary>
    /// SHA-256 do arquivo, em hex minúsculo.
    ///
    /// Lê em fluxo porque os datasets vão a 16 MB e carregá-los inteiros só para
    /// tirar o hash dobraria o pico de memória do boot — que roda no contêiner
    /// junto da API.
    /// </summary>
    private static async Task<string> ArquivoFingerprint(string path)
    {
        await using var fs = File.OpenRead(path);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static string? Str(JsonElement o, string p) =>
        o.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    // Tolerante: aceita Number que não seja Int32 limpo (ex.: 66.65, ou fora do
    // range) — arredonda em vez de estourar FormatException no GetInt32().
    private static int? Int(JsonElement o, string p)
    {
        if (!o.TryGetProperty(p, out var v) || v.ValueKind != JsonValueKind.Number) return null;
        if (v.TryGetInt32(out var i)) return i;
        if (v.TryGetDouble(out var d) && d >= int.MinValue && d <= int.MaxValue) return (int)Math.Round(d);
        return null;
    }

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
        // `codigo` é a PK mas NÃO é único na fonte: a mesma rua se repete por bairro
        // (ex.: cód 512 em 3 bairros) e há duplicatas exatas (ex.: cód 997 ×5).
        // Para o lookup de referência basta a 1ª ocorrência de cada código.
        var seen = new HashSet<int>();
        var chunk = new List<Street>(5000);
        var total = 0;
        var skipped = 0;
        foreach (var r in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            var codigo = r.GetProperty("codigo").GetInt32();
            if (!seen.Add(codigo)) { skipped++; continue; }
            chunk.Add(new Street
            {
                Codigo = codigo,
                Tipo = Str(r, "tipo") ?? "",
                Nome = Str(r, "nome") ?? "",
                Bairro = NullIfEmpty(Str(r, "bairro")),
                NumLei = Int(r, "numLei"),
                DataLei = NullIfEmpty(Str(r, "dataLei")),
                Localizacao = NullIfEmpty(Str(r, "localizacao")),
                Ext = Dbl(r, "ext"),
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
        if (skipped > 0)
            log.LogInformation("street: {n} linhas puladas (codigo repetido)", skipped);
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

    /// <summary>
    /// Postes de Blumenau, do formato posicional com dicionário.
    ///
    /// O JSON traz `campos` com a ordem das colunas — mas os índices aqui são
    /// fixos de propósito: se a ordem mudar na origem, é melhor o seed falhar de
    /// cara na conversão do que carregar 45 mil linhas com o bairro no lugar da
    /// estrutura. Os dicionários usam -1 para nulo.
    /// </summary>
    private static async Task<int> SeedPostes(DecrypterDbContext db, ILogger log, string path)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = doc.RootElement;

        string[] Dic(string nome) => root.TryGetProperty(nome, out var a)
            ? [.. a.EnumerateArray().Select(x => x.GetString() ?? "")]
            : [];
        var logradouros = root.GetProperty("logradouros").EnumerateArray().ToArray();
        var bairros = Dic("bairros");
        var estruturas = Dic("estruturas");
        var tipos = Dic("tipos");
        var situacoes = Dic("situacoes");
        var datas = Dic("datas");

        static string? Ref(string[] dic, JsonElement e)
        {
            var i = e.GetInt32();
            return i >= 0 && i < dic.Length ? dic[i] : null;
        }
        static int? Int(JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetInt32() : null;
        static DateTimeOffset? Data(string[] dic, JsonElement e)
        {
            var v = Ref(dic, e);
            // O portal não informa fuso; as datas são horário local de Blumenau.
            return v is null ? null : DateTime.SpecifyKind(DateTime.Parse(v), DateTimeKind.Utc);
        }

        var chunk = new List<Poste>(5000);
        var total = 0;
        foreach (var r in root.GetProperty("rows").EnumerateArray())
        {
            var log0 = logradouros[r[4].GetInt32()];
            chunk.Add(new Poste
            {
                Id = r[0].GetInt32(),
                Plaqueta = NullIfEmpty(r[1].GetString()),
                Lat = r[2].GetDouble(),
                Lng = r[3].GetDouble(),
                Rua = NullIfEmpty(log0[0].GetString()),
                RuaTipo = NullIfEmpty(log0[1].GetString()),
                RuaNome = NullIfEmpty(log0[2].GetString()),
                RuaId = Int(log0[3]),
                Numero = Int(r[5]),
                Bairro = Ref(bairros, r[6]),
                Estrutura = Ref(estruturas, r[7]),
                EstruturaId = Int(r[8]),
                Tipo = Ref(tipos, r[9]),
                Status = Ref(situacoes, r[10]),
                PontosLuminosos = Int(r[11]) is { } pl ? (short)pl : null,
                Altura = Int(r[12]),
                Instalacao = Data(datas, r[13]),
                Alteracao = Data(datas, r[14]),
                Cor = Int(r[15]),
            });
            if (chunk.Count >= 5000)
            {
                db.Postes.AddRange(chunk);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                total += chunk.Count;
                chunk.Clear();
            }
        }
        if (chunk.Count > 0)
        {
            db.Postes.AddRange(chunk);
            await db.SaveChangesAsync();
            total += chunk.Count;
        }
        return total;
    }


    /// <summary>Aeroportos (OpenFlights), formato posicional já na origem.</summary>
    private static async Task<int> SeedAirports(DecrypterDbContext db, ILogger log, string path)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var chunk = new List<Airport>(5000);
        var total = 0;
        var id = 0;
        foreach (var r in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            chunk.Add(new Airport
            {
                Id = ++id,
                // A origem usa "" para ausente; o índice parcial do schema conta
                // com isso (WHERE iata <> ''), então guardamos como veio.
                Iata = r[0].GetString() ?? "",
                Icao = r[1].GetString() ?? "",
                Nome = NullIfEmpty(r[2].GetString()),
                Cidade = NullIfEmpty(r[3].GetString()),
                Pais = NullIfEmpty(r[4].GetString()),
                Lat = r[5].ValueKind == JsonValueKind.Number ? r[5].GetDouble() : null,
                Lng = r[6].ValueKind == JsonValueKind.Number ? r[6].GetDouble() : null,
            });
            if (chunk.Count >= 5000)
            {
                db.Airports.AddRange(chunk);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                total += chunk.Count;
                chunk.Clear();
            }
        }
        if (chunk.Count > 0)
        {
            db.Airports.AddRange(chunk);
            await db.SaveChangesAsync();
            total += chunk.Count;
        }
        return total;
    }


    /// <summary>
    /// Rol de Ruas COMPLETO. Ao contrário de `SeedStreets`, **não deduplica por
    /// código**: as 415 linhas que ele descarta são a mesma rua em outro bairro,
    /// com localização e extensão próprias — informação, não repetição.
    /// </summary>
    private static async Task<int> SeedStreetRol(DecrypterDbContext db, ILogger log, string path)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var chunk = new List<StreetRol>(5000);
        var total = 0;
        var id = 0;
        foreach (var r in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            chunk.Add(new StreetRol
            {
                Id = ++id,
                Codigo = Int(r, "codigo") ?? 0,
                Tipo = Str(r, "tipo") ?? "",
                Nome = Str(r, "nome") ?? "",
                BairroNum = Int(r, "bairroNum"),
                Bairro = NullIfEmpty(Str(r, "bairro")),
                NumLei = Int(r, "numLei"),
                DataLei = NullIfEmpty(Str(r, "dataLei")),
                Localizacao = NullIfEmpty(Str(r, "localizacao")),
                Ext = Dbl(r, "ext"),
                Larg = Dbl(r, "larg"),
                Atas = NullIfEmpty(Str(r, "atas")),
                Lat = Dbl(r, "lat"),
                Lng = Dbl(r, "lng"),
            });
            if (chunk.Count >= 5000)
            {
                db.StreetRol.AddRange(chunk);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                total += chunk.Count;
                chunk.Clear();
            }
        }
        if (chunk.Count > 0)
        {
            db.StreetRol.AddRange(chunk);
            await db.SaveChangesAsync();
            total += chunk.Count;
        }
        return total;
    }

    /// <summary>Pontes nomeadas. Listas viram texto juntado — é o que se lê.</summary>
    /// <summary>
    /// CID-10 do DATASUS. O JSON é posicional, com capítulo e grupo em
    /// dicionários à parte: repetir "Doenças do aparelho circulatório" em cada
    /// uma das 14 mil linhas dobraria o arquivo para não dizer nada de novo.
    ///
    /// Na tabela eles voltam desnormalizados de propósito: são 22 capítulos e
    /// 275 grupos, e uma junção por linha custaria mais que os bytes que a
    /// normalização pouparia numa tabela que só se lê.
    /// </summary>
    private static async Task<int> SeedCid(DecrypterDbContext db, ILogger log, string path)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var raiz = doc.RootElement;

        // [[num, descrição], …] — o número é o do capítulo (1..22), não o índice.
        var capitulos = raiz.GetProperty("capitulos").EnumerateArray()
            .ToDictionary(c => c[0].GetInt32(), c => c[1].GetString() ?? "");
        var grupos = raiz.GetProperty("grupos").EnumerateArray()
            .Select(g => g.GetString() ?? "").ToArray();

        var chunk = new List<Cid>(5000);
        var total = 0;
        foreach (var r in raiz.GetProperty("rows").EnumerateArray())
        {
            var cap = r[2].GetInt32();
            var iGrupo = r[3].GetInt32();
            chunk.Add(new Cid
            {
                Codigo = r[0].GetString() ?? "",
                Descricao = r[1].GetString() ?? "",
                Capitulo = (short)cap,
                CapituloDesc = capitulos.GetValueOrDefault(cap, ""),
                // -1 = código sem grupo. Não acontece na base de 2008, mas um
                // capítulo novo sem bloco definido cairia aqui em vez de estourar.
                GrupoDesc = iGrupo >= 0 && iGrupo < grupos.Length ? grupos[iGrupo] : null,
                Classif = NullIfEmpty(r[4].GetString()),
                Sexo = NullIfEmpty(r[5].GetString()),
                NaoObito = r[6].GetInt32() == 1,
            });
            if (chunk.Count >= 5000)
            {
                db.Cids.AddRange(chunk);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                total += chunk.Count;
                chunk.Clear();
            }
        }
        if (chunk.Count > 0)
        {
            db.Cids.AddRange(chunk);
            await db.SaveChangesAsync();
            total += chunk.Count;
        }
        return total;
    }

    /// <summary>
    /// Lotes de Blumenau. Posicional com dicionário de rua e bairro — 84.539
    /// linhas repetem 3.654 nomes de rua e 35 bairros, e repetir o texto em
    /// cada uma dobraria o arquivo para não dizer nada de novo.
    /// </summary>
    private static async Task<int> SeedLotes(DecrypterDbContext db, ILogger log, string path)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var raiz = doc.RootElement;
        var ruas = raiz.GetProperty("ruas").EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
        var bairros = raiz.GetProperty("bairros").EnumerateArray().Select(x => x.GetString() ?? "").ToArray();

        static string? Nome(string[] tabela, int i) =>
            i >= 0 && i < tabela.Length && tabela[i].Length > 0 ? tabela[i] : null;

        var chunk = new List<LoteBlumenau>(5000);
        var total = 0;
        var id = 0;
        foreach (var r in raiz.GetProperty("rows").EnumerateArray())
        {
            chunk.Add(new LoteBlumenau
            {
                Id = ++id,
                Inscricao = NullIfEmpty(r[0].GetString()),
                Iq = NullIfEmpty(r[1].GetString()),
                Logradouro = Nome(ruas, r[2].GetInt32()),
                Numero = NullIfEmpty(r[3].GetString()),
                Bairro = Nome(bairros, r[4].GetInt32()),
                Cep = NullIfEmpty(r[5].GetString()),
                // 0 é o "sem geometria" do gerador — vira NULL, não um ponto no
                // golfo da Guiné.
                Lat = r[6].GetDouble() is var la && la != 0 ? la : null,
                Lng = r[7].GetDouble() is var lo && lo != 0 ? lo : null,
                AreaM2 = r[8].GetInt32(),
                // Décimo campo: o conjunto de endereços do lote de esquina,
                // vazio na maioria das linhas. Lê defensivamente porque um
                // arquivo gerado antes do `build:enderecos` tem nove campos, e
                // seed que estoura em arquivo velho trava o deploy inteiro (a
                // API só sobe depois do sidecar terminar).
                Enderecos = r.GetArrayLength() > 9 ? NullIfEmpty(r[9].GetString()) : null,
            });
            if (chunk.Count >= 5000)
            {
                db.LotesBlumenau.AddRange(chunk);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                total += chunk.Count;
                chunk.Clear();
            }
        }
        if (chunk.Count > 0)
        {
            db.LotesBlumenau.AddRange(chunk);
            await db.SaveChangesAsync();
            total += chunk.Count;
        }
        return total;
    }

    private static async Task<int> SeedBridges(DecrypterDbContext db, ILogger log, string path)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        static string? Lista(JsonElement r, string nome) =>
            r.TryGetProperty(nome, out var a) && a.ValueKind == JsonValueKind.Array && a.GetArrayLength() > 0
                ? string.Join(" · ", a.EnumerateArray().Select(x => x.ToString()))
                : null;

        var chunk = new List<Bridge>();
        var id = 0;
        foreach (var r in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            chunk.Add(new Bridge
            {
                Id = ++id,
                Nome = Str(r, "nome") ?? "",
                NomeOsm = NullIfEmpty(Str(r, "nomeOsm")),
                Apelidos = Lista(r, "apelidos"),
                Tipo = NullIfEmpty(Str(r, "tipo")),
                Fonte = NullIfEmpty(Str(r, "fonte")),
                Lei = NullIfEmpty(Str(r, "lei")),
                NumLei = Int(r, "numLei"),
                AnoLei = Int(r, "anoLei"),
                DataLei = NullIfEmpty(Str(r, "dataLei")),
                Ementa = NullIfEmpty(Str(r, "ementa")),
                UrlLei = NullIfEmpty(Str(r, "urlLei")),
                Situacao = NullIfEmpty(Str(r, "situacao")),
                Lat = Dbl(r, "lat"),
                Lng = Dbl(r, "lng"),
                Comprimento = Dbl(r, "comprimento"),
                Via = NullIfEmpty(Str(r, "via")),
                Material = NullIfEmpty(Str(r, "material")),
                Transpoe = Lista(r, "transpoe"),
                Bairros = Lista(r, "bairros"),
            });
        }
        db.Bridges.AddRange(chunk);
        await db.SaveChangesAsync();
        return chunk.Count;
    }

}
