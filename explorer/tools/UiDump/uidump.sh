#!/usr/bin/env bash
# Fast launcher for UiDump: runs the prebuilt DLL directly, skipping MSBuild project
# evaluation (~1-2s per `dotnet run`). Builds once if the DLL is missing/stale.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DLL="$(ls "$DIR"/bin/Release/net*/UiDump.dll 2>/dev/null | head -1 || true)"
if [ -z "$DLL" ] || [ "$DIR/Program.cs" -nt "$DLL" ]; then
    dotnet build -c Release "$DIR" >/dev/null
    DLL="$(ls "$DIR"/bin/Release/net*/UiDump.dll | head -1)"
fi
exec dotnet "$DLL" "$@"
