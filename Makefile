SHELL := /bin/bash

EXPLORER_DIR := explorer
SCENES_DIR   := $(EXPLORER_DIR)/scenes
TESTS_DIR    := $(EXPLORER_DIR)/Tests
WEB_DIR      := web

.PHONY: help \
        install install-scenes install-web \
        scenes-build scenes-new-scene scenes-format scenes-format-fix scenes-syncpack \
        explorer-build \
        web-test web-test-headed web-test-ui web-typecheck web-report \
        clean

## Show this help.
help:
	@awk ' \
		/^## / { gsub(/^## /, "", $$0); doc = doc $$0 "\n"; next } \
		/^[a-zA-Z][a-zA-Z0-9_-]*:/ && doc { \
			target = $$1; sub(/:$$/, "", target); \
			printf "\033[36m  %-28s\033[0m %s", target, doc; \
			doc = ""; next \
		} \
		{ doc = "" } \
	' $(MAKEFILE_LIST)

# ─────────────── install ───────────────────────────────────────────────────────

## Install everything (scenes + web).
install: install-scenes install-web

## Install scene workspace deps under explorer/scenes.
install-scenes:
	cd $(SCENES_DIR) && npm install

## Install web suite deps + Playwright browsers.
install-web:
	cd $(WEB_DIR) && npm install && npx playwright install chromium

# ─────────────── visual scenes (explorer/scenes) ───────────────────────────────

## Build every visual-regression scene in parallel.
scenes-build:
	cd $(SCENES_DIR) && npm run build

## Scaffold a new visual scene + matching C# fixture. Usage: make scenes-new-scene NAME=my-scene
scenes-new-scene:
ifndef NAME
	$(error NAME is not set. Usage: make scenes-new-scene NAME=my-scene)
endif
	cd $(SCENES_DIR) && npm run new-scene -- $(NAME)

## Run prettier --check across the scene workspace.
scenes-format:
	cd $(SCENES_DIR) && npm run format

## Run prettier --write across the scene workspace.
scenes-format-fix:
	cd $(SCENES_DIR) && npm run format:fix

## Report @dcl/* version mismatches across scene packages.
scenes-syncpack:
	cd $(SCENES_DIR) && npm run syncpack:list

# ─────────────── explorer (C# / NUnit) ─────────────────────────────────────────

## Build the C# Tests project.
explorer-build:
	dotnet build $(TESTS_DIR)

# ─────────────── web (Playwright) ──────────────────────────────────────────────

## Run the web suite (headless).
web-test:
	cd $(WEB_DIR) && npm test

## Run the web suite with a visible browser.
web-test-headed:
	cd $(WEB_DIR) && npm run test:headed

## Open Playwright's interactive UI mode for the web suite.
web-test-ui:
	cd $(WEB_DIR) && npm run test:ui

## Type-check the web suite.
web-typecheck:
	cd $(WEB_DIR) && npm run typecheck

## Open the last Playwright HTML report.
web-report:
	cd $(WEB_DIR) && npm run report

# ─────────────── housekeeping ──────────────────────────────────────────────────

## Remove all node_modules + scene bin/ + dotnet bin/obj.
clean:
	rm -rf $(SCENES_DIR)/node_modules
	rm -rf $(SCENES_DIR)/packages/*/bin
	rm -rf $(SCENES_DIR)/packages/*/main.crdt
	rm -rf $(WEB_DIR)/node_modules
	rm -rf $(TESTS_DIR)/bin $(TESTS_DIR)/obj
