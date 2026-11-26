# The SDK's install script puts dotnet in ~/.dotnet without adding it to PATH.
DOTNET ?= $(shell command -v dotnet 2>/dev/null || echo $(HOME)/.dotnet/dotnet)

.PHONY: run dev test

run: frontend/node_modules
	cd frontend && npm run build
	$(DOTNET) run --project Web

# Serves the page with live reload on http://localhost:5173; needs `make run` going in another terminal.
dev: frontend/node_modules
	cd frontend && npm run dev

test: frontend/node_modules
	$(DOTNET) test
	cd frontend && npm test && npm run typecheck

frontend/node_modules: frontend/package-lock.json
	cd frontend && npm ci
	touch $@
