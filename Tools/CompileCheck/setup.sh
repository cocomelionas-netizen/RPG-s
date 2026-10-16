#!/usr/bin/env bash
# One-time setup of the compile check (no Unity needed): the .NET SDK and Unity reference DLLs from NuGet.
set -e
REFS=${RPG_REFS:-/tmp/rpgrefs}
mkdir -p "$REFS/unityref" "$REFS/uiref"
if ! command -v dotnet >/dev/null && [ ! -x "$HOME/.dotnet/dotnet" ]; then
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 8.0 >/dev/null
fi
curl -sSL https://www.nuget.org/api/v2/package/UnityEngine.Modules/2021.3.33 -o "$REFS/u.nupkg"
curl -sSL https://www.nuget.org/api/v2/package/Unity3D.UnityEngine.UI/2018.3.5.1 -o "$REFS/ui.nupkg"
# UnityEditor.dll for editor-check.sh.
mkdir -p "$REFS/sdk"
curl -sSL https://www.nuget.org/api/v2/package/Unity3D.SDK/2021.1.14.1 -o "$REFS/sdk.nupkg"
(cd "$REFS/sdk" && unzip -qo ../sdk.nupkg)
(cd "$REFS/unityref" && unzip -qo ../u.nupkg)
(cd "$REFS/uiref" && unzip -qo ../ui.nupkg)
echo "Ready. Run: bash Tools/CompileCheck/check.sh"
