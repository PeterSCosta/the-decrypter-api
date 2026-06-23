# Convenções:
#   make sync-data        # copia JSONs de ../the-decrypter/public/data para seed-data/
#   make sync-data FRONT=/outro/caminho/the-decrypter
#   make validate-data    # roda jq -e . em cada JSON (CI usa antes do docker build)
#   make build            # docker build local
#   make dev-up / dev-down

FRONT ?= ../the-decrypter
DATA_SRC := $(FRONT)/public/data
DATA_DST := seed-data
JSONS := municipios.json streets.json ceps.json

.PHONY: sync-data validate-data build dev-up dev-down

sync-data:
	@test -d "$(DATA_SRC)" || (echo "ERRO: $(DATA_SRC) não existe. Clone the-decrypter ou use FRONT=..." && exit 1)
	@for f in $(JSONS); do \
	  echo "→ $$f"; \
	  cp "$(DATA_SRC)/$$f" "$(DATA_DST)/$$f"; \
	done

validate-data:
	@command -v jq >/dev/null || (echo "ERRO: jq não instalado" && exit 1)
	@for f in $(JSONS); do \
	  test -s "$(DATA_DST)/$$f" || (echo "ERRO: $(DATA_DST)/$$f vazio ou ausente" && exit 1); \
	  jq -e . "$(DATA_DST)/$$f" >/dev/null || (echo "ERRO: $(DATA_DST)/$$f não é JSON válido" && exit 1); \
	  echo "✓ $$f válido"; \
	done

build: sync-data validate-data
	docker build -t the-decrypter-api .

dev-up:
	docker compose -f docker-compose.yml up -d

dev-down:
	docker compose -f docker-compose.yml down
