# Local AltTester .NET driver

`AltTester-Driver.2.3.3-dcl.transport.3b2540f5.nupkg` is built from
[mikhail-dcl/AltTester-Unity-SDK, commit 3b2540f5](https://github.com/mikhail-dcl/AltTester-Unity-SDK/commit/3b2540f507264eeb675bc7787400af485f7e6986).
The .NET project is `Bindings~/dotnet/AltDriver/AltDriver.csproj`; it compiles
`Assets/AltTester/Runtime/AltDriver`. There is no separate .NET source repository.

The transport commit follows the Unity compatibility base `3c644489` without
changing upstream PR #1984. Relative to that base it changes only the two driver
communication files and their tests. Compared with the formerly consumed NuGet
2.3.0, this package also includes the fork's existing 2.3.3 baseline; it is not a
cherry-pick onto 2.3.0. The existing project dependencies and both net5.0 and
netstandard2.0 targets are retained.

The patch requires registration before declaring a connection ready, prevents
old close events from invalidating a replacement session, fails closed sends,
and isolates responses by connection/command without replaying interrupted
commands. It does not fix a stalled Unity process.

`NuGet.Config` maps the exact AltTester-Driver ID to this directory. The automation
project pins an exact prerelease version, so it cannot silently restore public
2.3.0 or a later release. Other dependencies still come from nuget.org. This
package is committed for evaluation, not published to a registry. Remove the
local source mapping and vendored package when moving to an upstream release.

`alttester-provenance.json` records source and package/DLL SHA-256 hashes.
`explorer/Driver.Transport.Tests` tests the packaged DLL, not a source project
reference; its transport test file is copied unchanged from the source commit.
CI uses a fresh restore cache. Automation also logs the actual loaded driver
assembly and SHA-256 before connection, making live consumption verifiable.

Build from a clean checkout at the recorded source commit with .NET SDK 10.0.303:

```powershell
dotnet pack 'Bindings~/dotnet/AltDriver/AltDriver.csproj' --configuration Release --output <output-directory> -p:PACKAGE_VERSION=2.3.3-dcl.transport.3b2540f5 -p:Version=2.3.3-dcl.transport.3b2540f5 -p:RepositoryCommit=3b2540f507264eeb675bc7787400af485f7e6986 -p:RepositoryUrl=https://github.com/mikhail-dcl/AltTester-Unity-SDK -p:ContinuousIntegrationBuild=true -p:PackageLicenseUrl=
```

Clearing the legacy license URL avoids NU5035; the upstream LICENSE remains
inside the package. A copy also covers the vendored regression source. Archive
metadata can differ on rebuild: hashes identify the exact committed artifact,
not a guarantee of byte-identical packaging on another host. Any replacement
must use a new version and updated provenance.

```text
dotnet test explorer/Driver.Transport.Tests/Driver.Transport.Tests.csproj --configuration Release
dotnet build explorer/Tests/Tests.csproj --configuration Release
```
