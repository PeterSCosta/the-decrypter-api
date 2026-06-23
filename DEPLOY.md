# Deploy — TheDecrypter.Api (Dokploy)

API .NET 10 (container `aspnet`). Mesmo padrão do **the-decrypter** (front) e do
**the-logic-lab**: imagem no Docker Hub → Dokploy via compose, na rede externa
compartilhada `logiclabnetwork` (reusa Postgres + Redis do the-logic-lab).

## 1. Build local (testar a imagem)
```bash
docker build -t the-decrypter-api .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__DecrypterDB="Host=host.docker.internal;Port=5433;Database=decrypter;Username=postgres;Password=postgres" \
  -e ConnectionStrings__Redis="host.docker.internal:6380" \
  the-decrypter-api            # http://localhost:8080/swagger
```

## 2. Banco: criar + schema + dados (uma vez)
1. No Postgres compartilhado, crie o banco: `CREATE DATABASE decrypter;`
2. Aplique o schema (idempotente): `psql "<conn>" -f db/schema.sql`
3. Popule os dados (lê os JSONs do front):
   ```bash
   dotnet run --project src/TheDecrypter.Api -- seed --data ../the-decrypter/public/data
   ```
   (Use a connection string do banco alvo — local apontando pro Postgres do Dokploy, ou rode pela própria stack.)

## 3. CI/CD
`.github/workflows/deploy.yml` — a cada push na `main`: builda e publica
`petercosta/the-decrypter-api:latest` no Docker Hub e, **se** os secrets do Dokploy
existirem, dispara o deploy pela API. Senão, faça **Redeploy** manual no painel.

**Secrets (GitHub → Settings → Secrets):** `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN`
e (opcional) `DOKPLOY_URL`, `DOKPLOY_API_KEY`, `DOKPLOY_COMPOSE_ID`.

## 4. Dokploy
1. **Create → Compose**, fonte = este repo, arquivo `docker-compose.prod.yml`.
2. **Environment** (mínimo):
   - `ConnectionStrings__DecrypterDB` = `Host=postgres;Port=5432;Database=decrypter;Username=postgres;Password=…`
   - `ConnectionStrings__Redis` = `redis:6379,password=…`
   - `ALLOWED_ORIGIN` = `https://arromba.thelogiclab.com.br`
3. **Domains**: `api.arromba.thelogiclab.com.br`, **Container Port = 8080**, HTTPS on.
4. Deploy. Confirme em `…/api/health` → `{"status":"ok"}` e `…/api/cnpj/00000000000191`.

## 5. Ligar o front
No the-decrypter, trocar as chamadas diretas (BrasilAPI etc.) por `https://api.arromba…/api/...`
— aí ganham cache + rate-limit do backend de graça.
