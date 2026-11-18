# IL.AttributeBasedDI.Visualizer

Visualization extension for IL.AttributeBasedDI, targeting .NET 10 and .NET 11.
ASP.NET Core APIs use the matching shared framework for each target.

Runtime package dependencies use the same bounded ranges on both targets:

- Newtonsoft.Json: `[13.0.4,14)` (13.0.4 inclusive, 14.0.0 exclusive).

Consumers may choose a compatible version inside these windows using direct package references or centrally managed versions with direct references/central transitive pinning as appropriate. Non-floating ranges normally resolve the lowest applicable version, not automatically the latest. The core library dependency comes from the project reference when packing; private build/test tools retain fixed versions.

Build both targets with the .NET 11 SDK:

```shell
dotnet build --configuration Release
```