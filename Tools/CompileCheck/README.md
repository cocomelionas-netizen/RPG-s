# Compile check

Builds `Assets/_Project/Scripts/Runtime` and the play mode tests with `dotnet` against Unity reference DLLs and small
stubs (`Stubs.cs`, `TestStubs.cs`) for packages that are not on NuGet (Netcode, Input System...). It only finds C#
errors; real tests run in CI.

    bash Tools/CompileCheck/setup.sh   # once per machine
    bash Tools/CompileCheck/check.sh   # "0 Error(s)" is good

New runtime files outside the folders listed in `sync.sh` must be added there. When code uses a new API of a stubbed
package, add it to `Stubs.cs`. Editor scripts are not compiled here.
