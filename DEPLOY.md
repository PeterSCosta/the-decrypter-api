# Deploy — TheDecrypter.Api (Dokploy)

Stack **autocontida**: Postgres 17 + Redis 7 privados ao `the-decrypter-api`,
em rede interna; a API fica também na `logiclabnetwork` para o Traefik rotear.
Imagem .NET 10 publicada no Docker Hub (`petercosta/the-decrypter-api`).

> **Identidades:** owner do GitHub = `PeterSCosta`; namespace do Docker Hub =
> `petercosta` (minúsculo, conta separada). O `deploy.yml` taggeia
> `petercosta/the-decrypter-api`; `DOCKERHUB_USERNAME` é a conta Docker Hub.

## 1. Build local (testar a imagem)
```bash
make sync-data            # copia ../the-decrypter/public/data/*.json → seed-data/
make validate-data        # jq -e . em cada JSON
make build                # docker build -t the-decrypter-api .
make dev-up               # infra dev (postgres 5433, redis 6380)

docker run --rm -p 8080:8080 \
  -e ConnectionStrings__DecrypterDB="Host=host.docker.internal;Port=5433;Database=decrypter;Username=postgres;Password=postgres" \
  -e ConnectionStrings__Redis="host.docker.internal:6380" \
  the-decrypter-api seed --data /app/data    # popula o PG dev (idempotente)

docker run --rm -p 8080:8080 \
  -e ConnectionStrings__DecrypterDB="Host=host.docker.internal;Port=5433;Database=decrypter;Username=postgres;Password=postgres" \
  -e ConnectionStrings__Redis="host.docker.internal:6380" \
  the-decrypter-api                           # API em http://localhost:8080
```

## 2. CI/CD
`.github/workflows/deploy.yml` — a cada push na `main`:
1. checkout deste repo + checkout do `PeterSCosta/the-decrypter` (fonte dos JSONs);
2. `make sync-data` + `make validate-data` (jq) — falha o build se um JSON for inválido;
3. build da imagem (datasets embutidos em `/app/data` + schema.sql em `/app/db/`);
4. push de duas tags no Docker Hub: `:latest` e `:<YYYYMMDDHHMMSS>` (rollback);
5. se os secrets do Dokploy existirem, dispara redeploy.

**Secrets obrigatórios (GitHub → Settings → Secrets → Actions):**
- `DOCKERHUB_USERNAME` — conta Docker Hub (`petercosta`).
- `DOCKERHUB_TOKEN` — token de push no Docker Hub.
- `GH_PAT_THE_DECRYPTER` — **fine-grained PAT** com `Contents: read` no repo
  `PeterSCosta/the-decrypter` (privado). Sem ele o checkout dos JSONs 404a no CI.
  Criar em https://github.com/settings/personal-access-tokens/new (resource owner
  `PeterSCosta`, repo `the-decrypter`, permissão `Contents: Read-only`).

**Secrets opcionais (auto-redeploy — todos os três precisam estar setados):**
`DOKPLOY_URL`, `DOKPLOY_API_KEY`, `DOKPLOY_COMPOSE_ID`.
> `COMPOSE_ID` só existe depois de criar a stack no Dokploy (passo 3); na primeira
> vez sobe pelo painel, pega o ID, e os pushes seguintes auto-redeployam.

## 3. Dokploy — criar a stack
1. **Create → Compose**, fonte = este repo, arquivo `docker-compose.prod.yml`.
2. **Environment** (mínimo):
   - **`JWT_SIGNING_KEY`** — **obrigatória, a stack não sobe sem ela.** Gere com
     `openssl rand -base64 48`. Abaixo de 32 bytes a API **recusa iniciar**, de
     propósito: chave curta só estoura no primeiro login, com o contêiner
     aparentemente saudável e o acesso inteiro caído.
   - **`ADMIN_EMAIL` / `ADMIN_SENHA`** — o primeiro administrador, criado no boot
     se ainda não existir (idempotente). **Sem ele a base zerada trava**: todo
     cadastro nasce pendente e só um admin aprova.
   - `ALLOWED_ORIGIN=https://arromba.thelogiclab.com.br`
   - (opcional) `JWT_HORAS=12` — validade do token.
   - (opcional) `POSTGRES_PASSWORD` — ver §6.1 antes de definir.
   - (opcional) `W3W_API_KEY=...` (vazia → `/api/what3words` retorna 404)

   > `REDIS_PASSWORD` não é mais lida: o commit `e0c37e6` tirou o `requirepass`
   > do compose. A instrução anterior estava desatualizada.
3. **Domains:** `apiarromba.thelogiclab.com.br` · Container Port **8080** · HTTPS on.
4. **Volumes** (Dokploy gerencia): `decrypter-pgdata`, `decrypter-redisdata`.
5. Confirme que `logiclabnetwork` existe no host (`docker network ls`).
6. Deploy.

### O que acontece no primeiro deploy
- `postgres` faz `initdb` → roda `01-schema.sql` como **superuser** → cria extensões
  (`pg_trgm`, `unaccent`), função `immutable_unaccent`, tabelas e índices GIN.
- `redis` sobe com senha + AOF.
- `decrypter-api-seed` sobe (one-shot), aplica `psql -f schema.sql` (idempotente)
  e roda o seeder; ao terminar, sai com 0.
- `decrypter-api` só sobe depois (`service_completed_successfully`) e o Traefik
  passa a rotear. Tráfego **nunca** chega antes do banco estar populado.

### Redeploys
- `postgres` mantém o volume; `initdb` **não** roda de novo.
- `decrypter-api-seed` reaplica `schema.sql` (idempotente — pega colunas/índices
  novos que entraram no PR). Seeder vê `seed_state.status='complete'` por tabela
  e sai em ~1s.
- `decrypter-api` rola-restart.

### Refresh do dataset (raro)
Quando os JSONs do front mudam de verdade:
```bash
# Dentro do painel Dokploy (terminal no postgres):
psql -U postgres -d decrypter \
  -c "TRUNCATE cep, street, municipio RESTART IDENTITY;" \
  -c "DELETE FROM seed_state WHERE table_name IN ('cep','street','municipio');"
# Redeploy → seed roda do zero.
```

## 4. Validar (curls)
```bash
curl -s https://apiarromba.thelogiclab.com.br/api/health
curl -s https://apiarromba.thelogiclab.com.br/api/cep/89010000           # source:"db"
curl -s "https://apiarromba.thelogiclab.com.br/api/streets/search?q=XV%20de%20Novembro"
curl -s https://apiarromba.thelogiclab.com.br/api/municipio/4202404      # Blumenau
curl -s "https://apiarromba.thelogiclab.com.br/api/geocode?q=Blumenau"
curl -s https://apiarromba.thelogiclab.com.br/api/cnpj/00000000000191
```

## 5. Backup (responsabilidade nova — não esqueça)
Tarefa agendada no Dokploy (ou cron no host). Atenção: o `>` é redirecionamento
do shell **do host** — o caminho `/backups/` precisa existir no host (criar
antes), **não** dentro do container:
```bash
mkdir -p /backups   # uma vez, no host
docker exec <decrypter-postgres-container> \
  pg_dump -Fc -U postgres decrypter > /backups/decrypter-$(date +%F).dump
```
**Retenção sugerida:** 14 dumps diários + 4 semanais.
**Testar restore** num DB de teste antes de precisar de verdade:
```bash
createdb -U postgres decrypter_test
pg_restore -U postgres -d decrypter_test /backups/decrypter-2026-06-23.dump
```

## 6. Cuidados

### 6.1 Ligar senha no Postgres (pendente, e agora importa)
O compose roda o PG em `POSTGRES_HOST_AUTH_METHOD=trust`, e a justificativa
escrita era *"dados públicos numa rede interna isolada"*. **Ela caiu**: desde que
`app_user` guarda hash de senha, qualquer contêiner que entre na
`decrypter-internal` lê a tabela sem apresentar credencial.

Definir `POSTGRES_PASSWORD` **não basta num volume já inicializado** — o
`pg_hba.conf` mora dentro do volume e continua em `trust`, então a variável não
surte efeito (e por isso também não quebra nada ao ser definida). O passo é
manual, uma vez:

```bash
# 1. terminal no decrypter-db, define a senha do papel existente
psql -U postgres -d decrypter -c "ALTER USER postgres PASSWORD 'a-senha-escolhida';"

# 2. exige senha nas conexões TCP (o socket local segue em trust)
sed -i 's/^host all all all trust$/host all all all scram-sha-256/' \
  /var/lib/postgresql/data/pg_hba.conf
psql -U postgres -c "SELECT pg_reload_conf();"
```

Depois defina `POSTGRES_PASSWORD` no Dokploy com o mesmo valor e faça o redeploy —
a connection string do `decrypter-api` e o `PGPASSWORD` do sidecar já a leem.
**Ordem importa:** senha no banco primeiro, variável depois. O inverso derruba a
API no próximo deploy.

### 6.2 Geral
- **Nunca** rodar `docker compose down -v` ou "Destroy stack" sem snapshot — apaga
  os volumes nomeados (`decrypter-pgdata`, `decrypter-redisdata`).
- Dokploy prefixa os volumes com o nome da stack — confirme com `docker volume ls`
  no host após o primeiro deploy.
- **Modo de deploy:** o stack precisa rodar no modo **git-source** do Dokploy
  (não "paste-only"). O serviço `postgres` faz bind-mount de `./db/schema.sql` para
  o `/docker-entrypoint-initdb.d/` — se a árvore não existir no host, o initdb
  monta vazio e a primeira subida vem sem schema. O sidecar de seed cobre depois,
  mas evite essa janela.
- **Rollback depois do apelido:** desde que existe conta sem e-mail (o `email`
  deixou de ser `NOT NULL`), voltar para uma imagem anterior por tag **não**
  restaura a restrição — `email text NOT NULL` mora dentro de um
  `CREATE TABLE IF NOT EXISTS`, que é no-op em base existente. Pior: a imagem
  antiga tem `AppUser.Email` não-anulável e estoura ao ler a primeira linha com
  e-mail NULL. Antes de um rollback assim, ou restaure dump, ou preencha os
  nulos (`UPDATE app_user SET email = nickname || '@sem-email.local' WHERE email IS NULL`).
- **`schema.sql` deve ser estritamente aditivo** (`CREATE … IF NOT EXISTS`,
  `CREATE OR REPLACE`, novos índices). O sidecar reaplica em **todo** deploy, com
  a API antiga ainda viva — qualquer `DROP COLUMN`/`ALTER TABLE` destrutivo roda
  contra o tráfego em produção. Mudanças destrutivas exigem janela de manutenção
  ou ferramenta de migration própria (EF Migrations/Flyway), fora do sidecar.
- **Restaurar dump em DB já populado:** se restaurar `pg_restore` num PG que já
  rodou o seed, popule também `seed_state` (uma linha 'complete' por tabela) para
  o seeder pular em vez de PK-colidir. Já é tratado pelo upgrade path no Seeder
  (count > 0 sem `seed_state` → marca complete), mas vale saber.
- Postgres/Redis **não** têm portas publicadas (rede `internal: true`); são
  alcançáveis só pela `decrypter-api`/`decrypter-api-seed`. Para inspeção,
  exec via Dokploy ou anexe um container temporário à `decrypter-internal`.
- Labels do Traefik são injetadas pelo painel **Domain** do Dokploy — se for
  rodar fora do Dokploy, adicione labels explícitas no `decrypter-api`.

## 7. Ligar o front (passo futuro)
No `the-decrypter`, introduzir `VITE_API_BASE_URL=https://apiarromba.thelogiclab.com.br`
e repontar os 5 arquivos que ainda chamam terceiros direto
(`brasilapi.ts`, `openfoodfacts.ts`, `geocode.ts`, `what3words.ts`, e o `loadPix` em
`data.ts`). CORS já está liberado para o domínio do front via `ALLOWED_ORIGIN`.
