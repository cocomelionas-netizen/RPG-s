#!/usr/bin/env bash
# Compiles the editor scripts (scene builder, generated data) with the runtime and tests against stubs: CI fails on any
# editor error, and check.sh does not compile them. Needs setup.sh once.
cd "$(dirname "$0")"
DOTNET=$(command -v dotnet || echo "$HOME/.dotnet/dotnet")
bash sync.sh
rm -rf Editor/src && mkdir -p Editor/src
cp src/*.cs Editor/src/
R=../../Assets/_Project/Scripts/Runtime
cp $R/Combat/TrainingDummy.cs $R/Combat/TargetIndicator.cs $R/Networking/DevBot.cs Editor/src/
# The build menu uses build APIs newer than the reference UnityEditor.dll.
find ../../Assets/_Project/Scripts/Editor -name "*.cs" ! -name BuildMenu.cs -exec cp {} Editor/src/ \;
# The tests' AssetDatabase stub would hide the real one.
grep -v "public static class AssetDatabase" TestStubs.cs \
  | sed 's/public class NetworkPrefabs {/public class NetworkPrefabs { public System.Collections.Generic.List<NetworkPrefabsList> NetworkPrefabsLists = new System.Collections.Generic.List<NetworkPrefabsList>();/' \
  > Editor/src/TestStubsForEditor.cs
(cd Editor && "$DOTNET" build -nologo 2>&1 | grep -E " error |Error\(s\)" | sort -u)
