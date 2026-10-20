#!/usr/bin/env bash
# Compiles the runtime scripts and play mode tests against stubs (catches C# errors before CI).
cd "$(dirname "$0")"
DOTNET=$(command -v dotnet || echo "$HOME/.dotnet/dotnet")
bash sync.sh || exit 1
# Prints the errors (and the "N Error(s)" line); exits 1 when the build failed or any error was printed.
output=$("$DOTNET" build -nologo 2>&1)
build=$?
echo "$output" | grep -E " error |Error\(s\)" | sort -u
errors=$(echo "$output" | grep -cE " error ")
# The reference DLLs are older than Unity 6.6: also look for APIs that are obsolete there (CI fails on them).
bash deprecations.sh
obsolete=$?
[ $build -eq 0 ] && [ "$errors" -eq 0 ] && [ $obsolete -eq 0 ] || exit 1
exit 0
