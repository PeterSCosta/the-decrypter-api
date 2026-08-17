# ADR — Backend do The Decrypter

**Status:** aceito · **Base:** espelha o `the-logic-lab` (.NET 10, Postgres, Redis, JWT/OIDC, Serilog/Seq, Prometheus).

## Contexto
O The Decrypter é hoje um SPA React 100% client-side: datasets em JSON (CEP 3 MB, ruas
0,9 MB…) e consultas externas direto do browser (limitado a APIs com CORS). Precisamos de:
**cache**, **controle de rate-limit** de provedores (ex.: ReceitaWS 3/min), **chaves
escondidas**, APIs **sem CORS** (Correios, gov.br Conecta, ReceitaWS) e **usuários/acesso**.

## Decisão
Migrar a **camada de dados + consultas externas** para um backend .NET que reusa a infra do
the-logic-lab. O React vira **cliente fino** que fala só com `/api/...`.

Camadas (iguais ao the-logic-lab): **Domain · Application · Ef · Cache · Http · Api · Test**.

### 1. Cache-first (Redis)
Toda consulta externa passa por `ICacheService` (mesmo contrato/serviço do the-logic-lab).
CNPJ/CEP mudam pouco → TTL longo (7 dias absoluto + 1 dia sliding). A maioria das chamadas
nunca toca o upstream — **isto sozinho já resolve a maior parte do limite de 3/min**.

### 2. Resiliência + rate-limit (Polly)
O `the-logic-lab` usa só `AddHttpClient()` (sem Polly). Aqui adicionamos
**`Microsoft.Extensions.Http.Resilience`** (Polly v8) em cada HttpClient tipado:
`AddResilienceHandler` com **timeout + retry(backoff+jitter) + circuit-breaker** e, por upstream,
um **`TokenBucketRateLimiter`** (ex.: `TokenLimit=3 / 1 min`). Ver `Http/HttpDependencyInjection.cs`.
- **Multi-réplica:** o token-bucket em memória limita por instância. Para limite **global**,
  trocar por um token-bucket no **Redis** (script Lua) — uma instância de `RateLimiter` custom.
- **429:** o retry honra `Retry-After`; se o bucket esvaziar, serve cache "stale" ou "tente em Xs".
- Dois limites distintos: **upstream** (Polly, proteger o provedor) e **por-usuário**
  (middleware de Rate Limiting do ASP.NET Core, proteger a gente).

### 3. Dados em PostgreSQL (em vez de JSON no cliente)
EF Core 10 + Npgsql (igual the-logic-lab). Tabelas `cep`, `street`, `municipio`, `app_user`
(ver `db/schema.sql`). Por que Postgres e não SQLite/JSON: você **já roda** Postgres; índices,
consulta parcial e zero download de 3 MB. Permite expandir CEP de "só SC" → **Brasil inteiro**.
- **CEP curinga `88xxx500`:** prefixo fixo (`88010%`) usa o índice da PK via `LIKE`, e o
  resto vira **regex POSIX** do Postgres (`code ~ '^88\d{3}500$'`). Brasil inteiro → particionar
  por UF. Ver `Ef/Repositories/CepRepository.cs`.
- **Busca por nome** (logradouro/bairro/rua): `pg_trgm` + `unaccent` (GIN) — fuzzy e sem acento.

### 4. Auth / acesso / usuários — **decisão revista (implementada)**
A decisão original era reusar o JWT/OIDC (Keycloak) do the-logic-lab. **Foi
superada:** o app tem um público pequeno e conhecido, e depender do Keycloak
acoplaria o acesso do Decrypter ao ciclo de vida de outro produto, para uma
necessidade que é "um punhado de pessoas que eu libero na mão".

O que existe hoje: **e-mail + senha próprios**, hash PBKDF2 (`IPasswordHasher`,
sem arrastar o ASP.NET Identity), **JWT HS256 emitido pela própria API**, e
**aprovação manual** — todo cadastro nasce `pendente` e só um admin libera.
`app_user` ganhou `password_hash`, `role`, `status`, `approved_at`/`approved_by`,
todos por `ALTER TABLE ... IF NOT EXISTS`.

Bearer e não cookie: o CORS já libera qualquer header, então o `Authorization`
passa sem mexer em política; cookie exigiria `AllowCredentials` dos dois lados.
O token vive em `localStorage` do app — exposto a XSS, compensado por validade
curta (12 h) e ausência de refresh token. É trade registrado, não descuido.

Todos os endpoints de dados são `[Authorize]`; só `/api/health` e as rotas de
`/api/auth` ficam anônimas. A chave de assinatura é validada **no boot** e a API
recusa subir se for curta demais.

Se um dia o Keycloak virar requisito (SSO entre os produtos), o caminho é trocar
o emissor mantendo `[Authorize]` e os papéis — a superfície já está no lugar.

## Fontes de dados (decisões)
- **Cidades (IBGE):** API Localidades (`/api/v1/localidades/municipios`) → 5.570 municípios +
  distritos. Metadados (população/área) via API **Agregados**. Fácil, ingestão em batch.
- **Bairros:** IBGE **não** tem base oficial de bairros. Melhor ativo = **dump nacional de
  CEP/DNE** (tem município + bairro + logradouro + coord do Brasil todo). Alternativas: IBGE
  CNEFE 2022, OSM (`place=suburb`). **gov.br Conecta CEP** (oficial, com credencial) = *fallback
  por-consulta* atrás do cache.
- **ReceitaWS:** grátis = 3/min → cache + Polly rate-limit (acima). CNPJ default: BrasilAPI
  (sem chave); trocar por ReceitaWS é só outra impl de `ICnpjGateway`.

## Caminho de migração (incremental)
1. `/api/cnpj/{n}` cache-first (Polly + Redis + BrasilAPI/ReceitaWS) — **PoC já neste repo**.
2. Datasets → Postgres + `/api/cep/search`, `/api/streets/search`. React para de baixar JSON.
3. Demais consultas externas atrás do gateway (ISBN, NCM, produto, w3w, gov.br Conecta).
4. Auth/usuários + rate-limit por usuário.
5. Ingestão CEP nacional + bairros.

## Consequências
- (+) Cache, rate-limit, chaves protegidas, APIs sem CORS, usuários, dados indexados.
- (−) Deixa de ser SPA zero-backend; +1 serviço (mas reusa Postgres/Redis/auth/observabilidade
  que já mantemos; Dokploy já tem os padrões de compose).
