# TheDecrypter.Api

Backend .NET 10 do The Decrypter — espelha a arquitetura do **the-logic-lab**
(camadas limpas, PostgreSQL/Npgsql, Redis, auth JWT). Responsabilidades:

1. **Dados em Postgres** (CEP, ruas, IBGE, usuários) — busca por curinga/fuzzy em SQL.
2. **Gateway de APIs externas** com **cache (Redis, cache-first)** + **resiliência Polly**
   (timeout, retry, circuit-breaker e **rate-limit por upstream**, ex.: ReceitaWS 3/min).

## Camadas
`Domain` (entidades/contratos) · `Application` (casos de uso) · `Ef` (Postgres) ·
`Cache` (Redis) · `Http` (Polly) · `Api` (controllers) · `Test`.

## Rodar local
```bash
docker compose up -d                 # Postgres (5433) + Redis (6380) + schema
cd src/TheDecrypter.Api
dotnet run                           # http://localhost:5080  (Swagger em /swagger)
```

### Endpoints
- `GET /api/health` → `{ "status": "ok" }`
- `GET /api/cnpj/{cnpj}` → empresa (cache-first; provedor rate-limited via Polly)
- `GET /api/cep/search?pattern=88xxx500` → CEPs que casam (Postgres; precisa de dados seedados)
- `GET /api/cep/export?pattern=88xxx500` → os mesmos CEPs, **todos**, em CSV para o Excel pt-BR
  (`;`, BOM UTF-8, vírgula decimal). Sem `.csv` na rota de propósito: a Cloudflare cacheia por
  extensão e serviria o arquivo a quem não mandou token.
- `GET /api/cep/{cep}` → CEP exato (base local de SC; se não achar, BrasilAPI)
- `GET /api/isbn/{isbn}` · `GET /api/ncm/{code}` · `GET /api/registrobr/{dominio}`
- `GET /api/produto/{barcode}` (Open Food Facts) · `GET /api/pix/{ispb}` (participante PIX)

Todas as consultas externas são **cache-first (Redis)** com resiliência **Polly**
(timeout · retry · circuit-breaker · rate-limit por upstream).

## Build & testes
```bash
dotnet build
dotnet test
```

Veja [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) para a decisão completa, o schema e a config do Polly.
