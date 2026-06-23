-- TheDecrypter — schema base (PostgreSQL). Idempotente. Rodado pelo initdb do
-- docker-compose (montado em /docker-entrypoint-initdb.d). Colunas em snake_case
-- batendo com o mapeamento do DecrypterDbContext.

CREATE EXTENSION IF NOT EXISTS pg_trgm;   -- busca fuzzy por nome (logradouro/bairro)
CREATE EXTENSION IF NOT EXISTS unaccent;  -- ignorar acentos (via wrapper IMMUTABLE)

-- unaccent é STABLE; pra usar em índice precisa de um wrapper IMMUTABLE:
CREATE OR REPLACE FUNCTION immutable_unaccent(text)
  RETURNS text LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
  AS $$ SELECT public.unaccent('public.unaccent', $1) $$;

-- ============================ CEP ============================
CREATE TABLE IF NOT EXISTS cep (
  code           varchar(8) PRIMARY KEY,            -- 8 dígitos
  logradouro     text,
  bairro         text,
  localidade     text,
  municipio_ibge integer,
  uf             char(2) NOT NULL,
  lat            double precision,
  lng            double precision
);
-- Curinga "88xxx500": o prefixo fixo (88010%) usa o índice da PK (LIKE 'prefixo%'),
-- e o restante vira regex POSIX no Postgres (code ~ '^88\d{3}500$').
CREATE INDEX IF NOT EXISTS ix_cep_uf ON cep (uf);
CREATE INDEX IF NOT EXISTS ix_cep_logradouro_trgm
  ON cep USING gin (immutable_unaccent(logradouro) gin_trgm_ops);
CREATE INDEX IF NOT EXISTS ix_cep_bairro_trgm
  ON cep USING gin (immutable_unaccent(bairro) gin_trgm_ops);
-- Para o Brasil inteiro (~1,2 M linhas) considere:  PARTITION BY LIST (uf).

-- ===================== Ruas (Rol de Ruas) =====================
CREATE TABLE IF NOT EXISTS street (
  codigo      integer PRIMARY KEY,
  tipo        text NOT NULL,
  nome        text NOT NULL,
  bairro      text,
  num_lei     integer,
  data_lei    text,           -- dd/mm/aaaa
  localizacao text,
  ext         double precision,   -- extensão em metros (valores fracionários no dataset)
  larg        double precision
);
-- Widening idempotente: tabelas criadas por uma versão anterior do schema tinham
-- `ext integer`. Rodado pelo sidecar de seed em todo deploy; no-op se já for double.
ALTER TABLE street ALTER COLUMN ext TYPE double precision;
CREATE INDEX IF NOT EXISTS ix_street_num_lei ON street (num_lei);
CREATE INDEX IF NOT EXISTS ix_street_nome_trgm
  ON street USING gin (immutable_unaccent(nome) gin_trgm_ops);

-- ===================== Municípios (IBGE) ======================
CREATE TABLE IF NOT EXISTS municipio (
  codigo_ibge integer PRIMARY KEY,   -- 7 dígitos
  nome        text NOT NULL,
  uf          char(2) NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_municipio_uf ON municipio (uf);
CREATE INDEX IF NOT EXISTS ix_municipio_nome_trgm
  ON municipio USING gin (immutable_unaccent(nome) gin_trgm_ops);

-- ========== seed_state (controle de seed self-healing) ==========
-- Usada pelo Seeder para detectar execução anterior incompleta (status='in_progress'
-- → TRUNCATE + refazer) e pular tabelas já completas (status='complete').
CREATE TABLE IF NOT EXISTS seed_state (
  table_name  text PRIMARY KEY,
  status      text NOT NULL,             -- 'in_progress' | 'complete'
  rows_loaded integer,
  finished_at timestamptz
);

-- ===== Usuários (futuro: acesso + limite por usuário/tenant) =====
CREATE TABLE IF NOT EXISTS app_user (
  id           uuid PRIMARY KEY,
  email        text NOT NULL,
  display_name text,
  created_at   timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_app_user_email ON app_user (email);
