#!/usr/bin/env bash
#
# Sobe a API local com um admin de DESENVOLVIMENTO já semeado.
#
# Existe porque, depois que o login entrou, o front local passou a cair na tela
# de entrada e não havia como testar nada de ponta a ponta sem uma conta. A
# alternativa — apontar o `.env.local` para a API de produção — é pior: mistura
# dado real com teste e gasta cota de verdade.
#
# ── A CREDENCIAL ABAIXO É DE BRINQUEDO, E DE PROPÓSITO ──────────────────────
# Ela vive num Postgres em contêiner, na porta 5433, acessível só de localhost,
# num banco que se apaga com `docker compose down -v`. Não é segredo, não vai
# para produção e não deve ser reaproveitada em lugar nenhum. Está escrita aqui
# justamente para ninguém precisar inventar outra nem guardá-la em canto torto.
#
#   apelido: dev          (ou o e-mail dev@local — o login aceita os dois)
#   senha:   dev12345
#
# (Oito caracteres porque a própria API recusa menos — a mesma regra vale em
# dev e em produção, e é bom que valha: um ambiente de teste com validação
# mais frouxa esconde justamente o erro que aparece no deploy.)
#
# A chave JWT é gerada a cada execução: nada de token de dev sobrevivendo a um
# reinício e virando hábito.
#
# Uso:
#   ./scripts/dev.sh          sobe infra + API (Ctrl-C encerra a API)
#   ./scripts/dev.sh --infra  só o Postgres e o Redis
set -euo pipefail
cd "$(dirname "$0")/.."

export PATH="/usr/local/share/dotnet:$PATH"

echo "▸ Subindo Postgres e Redis…"
docker compose up -d postgres redis >/dev/null

# O schema só roda no initdb, ou seja, na PRIMEIRA vez que o volume nasce. Num
# banco que já existe, reaplicar é idempotente e barato — e é o que garante que
# uma coluna nova do schema chegue no ambiente de quem já tinha o volume.
echo "▸ Aplicando o schema (idempotente)…"
until docker compose exec -T postgres pg_isready -U postgres -d decrypter >/dev/null 2>&1; do
  sleep 1
done
# `ON_ERROR_STOP=1` para o local FALHAR como a produção falha: o sidecar de
# deploy usa exatamente esta flag, e sem ela um erro de DDL aqui vira só um aviso
# no terminal — o mesmo arquivo que passou em dev derrubaria o deploy, e com ele
# a subida da API nova.
docker compose exec -T postgres psql -U postgres -d decrypter -q -v ON_ERROR_STOP=1 < db/schema.sql

if [ "${1:-}" = "--infra" ]; then
  echo "▸ Pronto. Postgres em 5433, Redis em 6380."
  exit 0
fi

echo "▸ Subindo a API em http://localhost:5080 …"
echo "  admin de dev:  dev  (ou dev@local)  /  dev12345"
echo

ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__DecrypterDB="Host=localhost;Port=5433;Database=decrypter;Username=postgres;Password=postgres" \
ConnectionStrings__Redis="localhost:6380" \
Jwt__SigningKey="$(openssl rand -base64 48)" \
Admin__Email="dev@local" \
Admin__Apelido="dev" \
Admin__Senha="dev12345" \
AllowedOrigins__0="http://localhost:5173" \
  dotnet run --project src/TheDecrypter.Api --urls "http://localhost:5080"
