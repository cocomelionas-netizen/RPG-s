#!/usr/bin/env bash
# Compiles the runtime scripts and play mode tests against stubs (catches C# errors before CI).
cd "$(dirname "$0")"
DOTNET=$(command -v dotnet || echo "$HOME/.dotnet/dotnet")
bash sync.sh && "$DOTNET" build -nologo 2>&1 | grep -E " error |Error\(s\)" | sort -u
# The reference DLLs are older than Unity 6.6: also look for APIs that are obsolete there (CI fails on them).
bash deprecations.sh
