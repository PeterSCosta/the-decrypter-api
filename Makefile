# Convenções:
#   make sync-data        # copia JSONs de ../the-decrypter/public/data para seed-data/
#   make sync-data FRONT=/outro/caminho/the-decrypter
#   make validate-data    # roda jq -e . em cada JSON (CI usa antes do docker build)
#   make build            # docker build local
#   make dev-up / dev-down

FRONT ?= ../the-decrypter
DATA_DST := seed-data
JSONS := municipios.json streets.json ceps.json postes.json airports.json

# Os datasets estão migrando de `public/data` (servido ao navegador) para
# `seed-data` (só fonte de seed). Procuramos nos dois: enquanto a migração não
# fecha, `postes.json` já está no novo e o resto ainda no antigo — e um `cp` de
# caminho fixo quebraria todo deploy no dia da mudança.

.PHONY: sync-data validate-data build dev-up dev-down

sync-data:
	@test -d "$(FRONT)" || (echo "ERRO: $(FRONT) não existe. Clone the-decrypter ou use FRONT=..." && exit 1)
	@for f in $(JSONS); do \
	  src=""; \
	  for d in "$(FRONT)/seed-data" "$(FRONT)/public/data"; do \
	    if [ -f "$$d/$$f" ]; then src="$$d/$$f"; break; fi; \
	  done; \
	  test -n "$$src" || (echo "ERRO: $$f não achado em seed-data/ nem em public/data/" && exit 1); \
	  echo "→ $$f  ($$src)"; \
	  cp "$$src" "$(DATA_DST)/$$f"; \
	done

validate-data:
	@command -v jq >/dev/null || (echo "ERRO: jq não instalado" && exit 1)
	@for f in $(JSONS); do \
	  test -s "$(DATA_DST)/$$f" || (echo "ERRO: $(DATA_DST)/$$f vazio ou ausente" && exit 1); \
	  jq -e . "$(DATA_DST)/$$f" >/dev/null || (echo "ERRO: $(DATA_DST)/$$f não é JSON válido" && exit 1); \
	  n=$$(jq -r '.rows | length' "$(DATA_DST)/$$f"); \
	  test "$$n" -gt 90 || (echo "ERRO: $(DATA_DST)/$$f só tem $$n linhas — truncado?" && exit 1); \
	  echo "✓ $$f válido ($$n linhas)"; \
	done

build: sync-data validate-data
	docker build -t the-decrypter-api .

dev-up:
	docker compose -f docker-compose.yml up -d

dev-down:
	docker compose -f docker-compose.yml down
