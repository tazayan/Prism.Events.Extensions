# Prism.Events.Extensions
Prism.Events.Extensions extends the official Prism.Events framework with async ValueTask-based event subscriptions and a lower-allocation publish pipeline designed to improve runtime performance and memory efficiency.

## Build

The repository pins its .NET SDK in `global.json`, targets .NET 8 and .NET 9 from `Directory.Build.props`, and manages dependency versions centrally in `Directory.Packages.props`.

```powershell
dotnet restore Prism.Events.Extensions.slnx
dotnet build Prism.Events.Extensions.slnx --configuration Release --no-restore
dotnet test tests/Prism.Events.Extensions/Prism.Events.Extensions.Tests.csproj --configuration Release --no-build
```

## Create a NuGet package

```powershell
dotnet pack src/Prism.Events.Extensions/Prism.Events.Extensions.csproj --configuration Release
```

Packages are written to `artifacts/packages`. The package ID currently defaults to the project name and can be set later with the `PackageId` MSBuild property.

Release assemblies contain embedded portable PDBs with Source Link metadata. Supported IDEs can therefore step into the package and retrieve the exact matching source files from GitHub without requiring a separate symbol package.
