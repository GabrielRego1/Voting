# Local automation for the Voting API.
# Usage: `make help`. Variables can be overridden, e.g. `make load RATE=5000 DURATION=20`.
# Voting closes 30 s after opening, so DURATION + RESULT_TIMEOUT must exceed ~35 s for the result check.

CONFIGURATION  ?= Release
BASE_URL       ?= http://localhost:5080
RATE           ?= 10000
DURATION       ?= 35
DISTRIBUTION   ?= 50,30,20
RETRY_FRACTION ?= 0.05
OUTPUT         ?= text
RESULT_TIMEOUT ?= 15
API_LOG        ?= artifacts/api.log

SOLUTION := Voting.slnx
API_BIN  := src/WebApi/bin/$(CONFIGURATION)/net10.0

.DEFAULT_GOAL := help
.ONESHELL:
SHELL := /bin/bash
.SHELLFLAGS := -eu -o pipefail -c

.PHONY: help setup test test-unit test-integration run load stress down

help: ## List available targets
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk -F':.*## ' '{printf "  %-17s %s\n", $$1, $$2}'

setup: ## Check prerequisites, start PostgreSQL and build the solution
	@command -v dotnet >/dev/null || { echo "error: .NET SDK 10 not found"; exit 1; }
	@command -v docker >/dev/null || { echo "error: docker not found"; exit 1; }
	@docker info >/dev/null 2>&1 || { echo "error: Docker daemon is not running"; exit 1; }
	docker compose up -d --wait postgres
	dotnet restore $(SOLUTION)
	dotnet build $(SOLUTION) -c $(CONFIGURATION) --no-restore

test: ## Run unit and integration tests (Testcontainers starts its own PostgreSQL)
	dotnet test --solution $(SOLUTION) -c $(CONFIGURATION)

test-unit: ## Run only the unit tests (no Docker needed)
	dotnet test --project tests/UnitTests -c $(CONFIGURATION)

test-integration: ## Run only the integration tests (Testcontainers starts its own PostgreSQL)
	dotnet test --project tests/IntegrationTests -c $(CONFIGURATION)

run: setup ## Start the API in the foreground for manual testing
	cd $(API_BIN)
	ASPNETCORE_ENVIRONMENT=Development exec dotnet WebApi.dll

load: setup ## Start the API in the background and run the load generator against it
	@mkdir -p $(dir $(API_LOG))
	if curl -s -o /dev/null $(BASE_URL); then
	  echo "error: something is already listening on $(BASE_URL); stop it first"; exit 1
	fi
	log=$$(realpath $(API_LOG))
	(cd $(API_BIN) && ASPNETCORE_ENVIRONMENT=Development exec dotnet WebApi.dll) >"$$log" 2>&1 &
	api_pid=$$!
	trap 'kill $$api_pid 2>/dev/null || true; wait $$api_pid 2>/dev/null || true' EXIT
	echo "API started (pid $$api_pid, log $(API_LOG)); waiting for it to accept requests..."
	curl -s -o /dev/null --retry-connrefused --retry 30 --retry-delay 1 \
	  $(BASE_URL)/polls/00000000-0000-0000-0000-000000000000 \
	  || { echo "error: API did not start; see $(API_LOG)"; exit 1; }
	echo "API ready; running load (~$(DURATION) s of votes + up to $(RESULT_TIMEOUT) s for the result)..."
	dotnet run --project tools/Workload -c $(CONFIGURATION) --no-build -- \
	  --base-url $(BASE_URL) --rate $(RATE) --duration $(DURATION) \
	  --distribution $(DISTRIBUTION) --retry-fraction $(RETRY_FRACTION) --result-timeout $(RESULT_TIMEOUT) --output $(OUTPUT)

stress: ## Find the max sustainable vote rate (recreates the local DB); e.g. `make stress BENCH_VERSION=v1`
	tools/stress/stress.sh

down: ## Stop PostgreSQL
	docker compose down
