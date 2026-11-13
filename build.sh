#!/usr/bin/env bash
# The whole gate. Nothing here calls the Claude API: the tests use a scripted gateway.
set -euo pipefail
cd "$(dirname "$0")"

echo "== build (warnings are errors)"
dotnet build Extraction.slnx --nologo -v quiet -clp:ErrorsOnly

echo "== tests"
dotnet test --project tests/Ticketdesk.Extraction.Tests --no-build 2>&1 | tail -n 6

echo "== build passed"
