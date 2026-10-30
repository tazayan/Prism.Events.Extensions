# Prism.Events.Extensions

`Prism.Events.Extensions` extends `Prism.Events` with `ValueTask`-based asynchronous event subscriptions and a lower-allocation publish pipeline designed to improve runtime performance and memory efficiency.

This is an independent extension package and is not an official PrismLibrary package.

## Install

```powershell
dotnet add package Async.Prism.Events.Extensions
```

## Build

The repository pins its .NET SDK in `global.json`, targets .NET 8, .NET 9, and .NET 10 from `Directory.Build.props`, and manages dependency versions centrally in `Directory.Packages.props`.

```powershell
dotnet restore Prism.Events.Extensions.slnx
dotnet build Prism.Events.Extensions.slnx --configuration Release --no-restore

$frameworks = dotnet msbuild tests/Prism.Events.Extensions/Prism.Events.Extensions.Tests.csproj -nologo -getProperty:TargetFrameworks
$frameworks.Split(';') | ForEach-Object {
    dotnet test tests/Prism.Events.Extensions/Prism.Events.Extensions.Tests.csproj --configuration Release --framework $_ --no-build
}
```

## Create a NuGet package

```powershell
dotnet pack src/Prism.Events.Extensions/Prism.Events.Extensions.csproj --configuration Release
```

Packages are written to `artifacts/packages`. Release assemblies contain embedded portable PDBs with Source Link metadata, so supported IDEs can retrieve the exact matching source files from GitHub while debugging without a separate symbol package.
