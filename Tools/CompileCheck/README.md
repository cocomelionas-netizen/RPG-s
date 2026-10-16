# Compile check

Builds `Assets/_Project/Scripts/Runtime` and the play mode tests with `dotnet` against Unity reference DLLs and small
stubs (`Stubs.cs`, `TestStubs.cs`) for packages that are not on NuGet (Netcode, Input System...). It only finds C#
errors; real tests run in CI.

    bash Tools/CompileCheck/setup.sh   # once per machine
    bash Tools/CompileCheck/check.sh   # "0 Error(s)" is good

New runtime files outside the folders listed in `sync.sh` must be added there. When code uses a new API of a stubbed
package, add it to `Stubs.cs`.

`editor-check.sh` also compiles the editor scripts (the scene builder and its generated data, not `BuildMenu.cs`) against
`UnityEditor.dll` (from the `Unity3D.SDK` NuGet package, fetched by `setup.sh`), with `Editor/EditorStubs.cs` for URP and the
Input System UI module. Run it after builder changes: CI fails on any editor error.

    bash Tools/CompileCheck/editor-check.sh   # "0 Error(s)" is good
