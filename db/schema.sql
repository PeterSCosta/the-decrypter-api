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

-- ========== poste (iluminação pública de Blumenau) ==========
-- 45.285 pontos coletados do portal Cidade Iluminada (Exati/IPBL).
CREATE TABLE IF NOT EXISTS poste (
  id               integer PRIMARY KEY,
  -- TEXT, não integer: 14 plaquetas têm zero à esquerda ("0338") e uma é
  -- "66688z". Como número, "0338" viraria 338 — outro poste.
  plaqueta         text,
  lat              double precision NOT NULL,
  lng              double precision NOT NULL,
  rua              text,
  rua_tipo         text,
  rua_nome         text,
  rua_id           integer,
  numero           integer,
  bairro           text,
  estrutura        text,
  estrutura_id     integer,
  tipo             text,
  status           text,
  pontos_luminosos smallint,
  altura           integer,
  instalacao       timestamptz,
  alteracao        timestamptz,
  cor              integer,
  -- Coordenada projetada para busca por proximidade. A longitude é pré-escalada
  -- por cos(26,9°) para a distância euclidiana do GiST ficar proporcional à
  -- métrica; metros ≈ graus × 111320.
  --
  -- A CONSTANTE É ÚNICA E COMPARTILHADA DE PROPÓSITO. Trocar por
  -- `cos(radians(lat))` (por linha) compila, é IMMUTABLE e está ERRADO: cada
  -- linha escalada pelo próprio cosseno introduz ~49 × Δcos ≈ 0,04° de
  -- deslocamento fantasma entre dois pontos — cerca de 4 km.
  coord_bnu        point GENERATED ALWAYS AS (point(lat, lng * 0.8918)) STORED
);

-- KNN de verdade: o core do Postgres traz `point_ops` para GiST com `<->` como
-- ordering operator, então `ORDER BY coord_bnu <-> point(?,?) LIMIT n` é varredura
-- por índice, sem PostGIS. O mesmo índice atende a caixa (`coord_bnu <@ box(...)`),
-- por isso NÃO existe btree em (lat,lng): seria um segundo índice para o mesmo
-- acesso.
CREATE INDEX IF NOT EXISTS ix_poste_coord_gist ON poste USING gist (coord_bnu);
CREATE INDEX IF NOT EXISTS ix_poste_plaqueta ON poste (plaqueta);
CREATE INDEX IF NOT EXISTS ix_poste_rua_trgm
  ON poste USING gin (immutable_unaccent(rua) gin_trgm_ops);
CREATE INDEX IF NOT EXISTS ix_poste_bairro ON poste (bairro);

-- ========== street_rol (Rol de Ruas COMPLETO) ==========
-- A tabela `street` usa `codigo` como PK, e isso engole 415 das 4.426 linhas: a
-- mesma rua existe em vários bairros, com `localizacao` e extensão diferentes em
-- cada um. Ela continua servindo `/api/streets/search` (consulta por chave, onde
-- a fusão não incomoda); esta guarda o rol INTEIRO, com os campos que faltavam
-- lá — `bairro_num`, `atas`, `lat` e `lng`.
--
-- Tabela nova em vez de alterar a PK de `street`: o sidecar de seed reaplica
-- este arquivo em todo deploy, com a API antiga viva, e trocar chave primária é
-- justamente o DDL destrutivo que o DEPLOY.md §6 proíbe nesse caminho.
CREATE TABLE IF NOT EXISTS street_rol (
  id          integer PRIMARY KEY,   -- sintética: o rol não tem chave própria
  codigo      integer NOT NULL,
  tipo        text NOT NULL,
  nome        text NOT NULL,
  bairro_num  integer,
  bairro      text,
  num_lei     integer,
  data_lei    text,
  localizacao text,
  ext         double precision,
  larg        double precision,
  atas        text,
  lat         double precision,
  lng         double precision
);
CREATE INDEX IF NOT EXISTS ix_street_rol_codigo ON street_rol (codigo);
CREATE INDEX IF NOT EXISTS ix_street_rol_nome_trgm
  ON street_rol USING gin (immutable_unaccent(nome) gin_trgm_ops);

-- ========== bridge (pontes, passarelas e viadutos nomeados) ==========
CREATE TABLE IF NOT EXISTS bridge (
  id          integer PRIMARY KEY,
  nome        text NOT NULL,
  nome_osm    text,
  apelidos    text,
  tipo        text,
  fonte       text,
  lei         text,
  num_lei     integer,
  ano_lei     integer,
  data_lei    text,
  ementa      text,
  url_lei     text,
  situacao    text,
  lat         double precision,
  lng         double precision,
  comprimento double precision,
  via         text,
  material    text,
  transpoe    text,
  bairros     text
);
CREATE INDEX IF NOT EXISTS ix_bridge_nome_trgm
  ON bridge USING gin (immutable_unaccent(nome) gin_trgm_ops);

-- ========== airport (OpenFlights) ==========
-- 7.599 aeroportos. Consulta por chave exata (IATA de 3 ou ICAO de 4 letras);
-- era um `find` linear sobre a base inteira, baixada no navegador.
CREATE TABLE IF NOT EXISTS airport (
  -- Chave sintética: a origem não tem uma. Nem todo aeroporto tem IATA e há
  -- ICAO repetido, então nenhum código serve de PK — e o EF Core não insere em
  -- entidade sem chave (`HasNoKey` é só para leitura).
  id      integer PRIMARY KEY,
  iata    text,
  icao    text,
  nome    text,
  cidade  text,
  pais    text,
  lat     double precision,
  lng     double precision
);
-- Sem PK: nem todo aeroporto tem IATA, e há ICAO repetido na base de origem.
-- Os dois índices são parciais para não indexar as centenas de vazios.
CREATE UNIQUE INDEX IF NOT EXISTS ux_airport_iata ON airport (iata) WHERE iata <> '';
CREATE INDEX IF NOT EXISTS ix_airport_icao ON airport (icao) WHERE icao <> '';

-- ===== Usuários: acesso ao app, com aprovação manual =====
CREATE TABLE IF NOT EXISTS app_user (
  id           uuid PRIMARY KEY,
  email        text NOT NULL,
  display_name text,
  created_at   timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_app_user_email ON app_user (email);

-- Colunas de acesso. Aditivas de propósito: o sidecar de seed reaplica este
-- arquivo em TODO deploy, com a API antiga ainda servindo tráfego (DEPLOY.md §6).
ALTER TABLE app_user ADD COLUMN IF NOT EXISTS password_hash text;
ALTER TABLE app_user ADD COLUMN IF NOT EXISTS role          text NOT NULL DEFAULT 'user';
ALTER TABLE app_user ADD COLUMN IF NOT EXISTS status        text NOT NULL DEFAULT 'pendente';
ALTER TABLE app_user ADD COLUMN IF NOT EXISTS approved_at   timestamptz;
ALTER TABLE app_user ADD COLUMN IF NOT EXISTS approved_by   uuid;

-- O índice único original é sensível a maiúscula: "Peter@x" e "peter@x" entravam
-- como duas contas, e a segunda nunca conseguiria logar de forma previsível. O
-- serviço normaliza para minúscula na escrita; este índice é a rede de segurança
-- para qualquer caminho que esqueça de normalizar.
CREATE UNIQUE INDEX IF NOT EXISTS ux_app_user_email_lower ON app_user (lower(email));

-- ===== Apelido: o identificador que não depende de ter e-mail =====
--
-- Some ADITIVAMENTE, e nesta ordem: a coluna primeiro, o índice depois. O
-- sidecar roda `psql -v ON_ERROR_STOP=1`, que executa statement a statement e
-- ABORTA no primeiro erro — um índice acima da coluna derrubaria o sidecar, e a
-- API nova não sobe (ela depende de `service_completed_successfully`).
ALTER TABLE app_user ADD COLUMN IF NOT EXISTS nickname text;

-- O e-mail deixa de ser obrigatório: quem se cadastra agora escolhe um apelido,
-- e o e-mail vira opcional. NÃO é destrutivo no sentido do DEPLOY.md §6 — não
-- apaga coluna nem dado, não reescreve a tabela e é re-executável —, e tem o
-- precedente do `ALTER COLUMN ext TYPE double precision` lá em cima.
--
-- As contas que já existem não perdem nada: continuam com o e-mail delas, e o
-- login aceita e-mail OU apelido no mesmo campo.
ALTER TABLE app_user ALTER COLUMN email DROP NOT NULL;

-- Único em minúsculas, e PARCIAL: as contas antigas ficam todas com nickname
-- NULL, e no Postgres NULL não colide com NULL — mas string vazia colide com
-- string vazia. O `<> ''` é o mesmo cinto que o índice de aeroporto usa, para o
-- dia em que algum caminho gravar branco em vez de NULL.
CREATE UNIQUE INDEX IF NOT EXISTS ux_app_user_nickname_lower
  ON app_user (lower(nickname)) WHERE nickname IS NOT NULL AND nickname <> '';

-- ========== cid (CID-10, DATASUS V2008) ==========
-- 14.233 códigos: 2.045 categorias (A00) + 12.188 subcategorias (A00.0).
--
-- `codigo` guarda a forma NORMALIZADA, sem ponto ("A000"), porque é a única que
-- os dois formatos do mundo real colapsam: o prontuário escreve "A00.0" e a
-- base do SUS escreve "A000". O ponto é reposto na exibição, e não no dado.
CREATE TABLE IF NOT EXISTS cid (
  codigo        varchar(4) PRIMARY KEY,
  descricao     text NOT NULL,
  capitulo      smallint NOT NULL,       -- 1..22
  capitulo_desc text NOT NULL,
  grupo_desc    text,
  -- Dupla classificação da CID: `+` (adaga) é a etiologia, `*` (asterisco) é a
  -- manifestação. Vazio na imensa maioria.
  classif       varchar(1),
  -- Restrição de sexo do código ("F"/"M"), quando existe.
  sexo          varchar(1),
  -- Verdadeiro quando o código NÃO pode ser causa básica de óbito.
  nao_obito     boolean NOT NULL DEFAULT false
);
-- A busca por doença é por NOME, e o nome vem acentuado e no meio da frase
-- ("diabetes" está em "Diabetes mellitus não especificado"): trigrama sobre o
-- texto sem acento é o que responde as duas coisas.
CREATE INDEX IF NOT EXISTS ix_cid_descricao_trgm
  ON cid USING gin (immutable_unaccent(descricao) gin_trgm_ops);
