#!/usr/bin/env bash
#
# Sets up a Decentraland test identity for the InWorld test suite.
#
# - First run: creates a fresh metaforge account named "$TEST_IDENTITY_NAME"
#   (BIP39 wallet, registers identity with the auth API, deploys a profile,
#    writes auth-token-bridge.txt so the Explorer auto-logs in).
# - Subsequent runs: re-applies the token bridge for the existing account
#   so the next `metaforge explorer run` skips the login screen.
#
# Usage:
#     scripts/setup-test-identity.sh           # uses default name
#     TEST_IDENTITY_NAME=my-qa scripts/setup-test-identity.sh
#
# After this, run InWorld tests with:
#     metaforge explorer run -- --alttester
#     metaforge explorer test --filter "Category=InWorld"

set -euo pipefail

NAME="${TEST_IDENTITY_NAME:-dcl-e2e-inworld}"

if ! command -v metaforge >/dev/null 2>&1; then
    echo "Error: metaforge CLI not found in PATH." >&2
    echo "Install it from the MetaForge repo or add its bin directory to PATH." >&2
    exit 1
fi

# Accounts are persisted in metaforge's local store; check whether ours exists.
if metaforge account list 2>/dev/null | grep -qE "(^| )${NAME}( |$)"; then
    echo "→ Identity '${NAME}' already exists. Refreshing auth token bridge…"
    metaforge account login "${NAME}" --auto-login
else
    echo "→ Creating new test identity '${NAME}'…"
    metaforge account create "${NAME}"
fi

echo
echo "✓ Test identity ready: ${NAME}"
echo "  The next 'metaforge explorer run' will boot straight in-world."
