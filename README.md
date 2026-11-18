[![NuGet version (IL.AttributeBasedDI)](https://img.shields.io/nuget/v/IL.AttributeBasedDI.svg?style=flat-square)](https://www.nuget.org/packages/IL.AttributeBasedDI/)
# IL.AttributeBasedDI
Control dependencies and decorators via custom attributes - extends Microsoft.Extensions.DependencyInjection.

> **Current targets:** .NET 10 and .NET 11. The bundled Roslyn analyzer remains `netstandard2.0` and uses Roslyn 5.0 APIs so it can load in both SDK 10 and SDK 11 compilers.

## Release versions

The tag-only publishing workflow validates the Git tag before restore and derives all package, assembly, file, and informational versions from it. Tags accept an optional `v`, three or four numeric components, and optional prerelease/build metadata, for example `v3.0.1-rc.1+build.7` or `3.0.1.2`. The core and Visualizer packages use the same tag-derived version, including the generated Visualizer dependency on core. Invalid tags fail before packaging; publishing remains restricted to tag builds.

NuGet normalizes package filenames and dependency minimums by omitting build metadata; the package manifest and informational version retain it. Assembly/file versions use the numeric core, padded to four components when needed.

Ordinary branch builds default to the harmless `0.0.0-dev` version. Explicit command-line MSBuild version properties remain supported. No hardcoded release-version bump in project files is needed.

## Consumer dependency versions

Runtime package dependencies use bounded NuGet ranges on both .NET 10 and .NET 11:

| Package | Inclusive minimum | Exclusive maximum |
| --- | --- | --- |
| IL.Misc (core) | 1.5.1.2 | 3.0.0 |
| IL.VirtualViews (Visualizer) | 1.9.2.4 | 3.0.0 |
| Newtonsoft.Json (Visualizer) | 13.0.4 | 14.0.0 |

Consumers can select a compatible version inside these windows with a direct package reference or a centrally managed version (and a direct reference or central transitive pinning where needed). These are non-floating ranges: NuGet normally resolves the lowest applicable version, not automatically the latest. Versions outside the windows are not supported by these dependency declarations. ASP.NET Core remains SDK-managed through a framework reference; private compiler/analyzer and test tools keep fixed versions.

## Build and test

Install the .NET 11 SDK to build all targets, plus the .NET 10 runtime to run the full test suite:

```shell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --no-restore
```

Tests run separately on `net10.0` and `net11.0`. ASP.NET Core APIs come from each target's shared framework; no cross-major framework package pins are needed. The analyzer deliberately uses Microsoft.CodeAnalysis.CSharp 5.0.0 instead of newer Roslyn APIs that SDK 10 cannot load.

---

# How to Use

1. Reference `IL.AttributeBasedDI` in your project.
2. Use the registration extensions provided by the library to activate functionality:
   - `services.AddServiceAttributeBasedDependencyInjection()` for `IServiceCollection`.
   - `builder.AddServiceAttributeBasedDependencyInjection()` for `WebApplicationBuilder`.
3. Optionally, filter assemblies for reflection search using the `assemblyFilters` parameter (e.g., `"MyProject.*"`).

---

# Attributes

## `[Service]`
Use this attribute to automatically register classes in the DI container.

### Parameters:
- **Lifetime**: Defines the service registration lifetime (`Singleton`, `Scoped`, or `Transient`).
- **ServiceType**: Specifies the service type for DI registration. If `null`, the service type is automatically resolved:
  - From the first interface the class implements, or
  - The class itself if no interfaces are implemented.
- **Key** (`.NET 8+`): Specifies a key for keyed service registration.
- **Feature** (optional): Specifies a feature flag to conditionally register the service.

---

## `[ServiceWithOptions]`
Use this attribute to automatically register classes with options from configuration in the DI container.

### Parameters:
- **Lifetime**: Defines the service registration lifetime (`Singleton`, `Scoped`, or `Transient`).
- **ServiceType**: Specifies the service type for DI registration. If `null`, the service type is automatically resolved:
  - From the first interface the class implements, or
  - The class itself if no interfaces are implemented.
- **Key** (`.NET 8+`): Specifies a key for keyed service registration.
- **Feature** (optional): Specifies a feature flag to conditionally register the service.

### How it works
The `ServiceWithOptions` attribute requires a generic type that implements the `IServiceConfiguration` interface. This interface has a static abstract property `ConfigurationPath` that defines the path to the configuration section in `appsettings.json`.

### Example

**`appsettings.json`**
```json
{
  "AppSettings": {
    "Test": {
      "Option1": "test12345"
    }
  }
}
```

**`ServiceTestOptions.cs`**
```csharp
public class ServiceTestOptions : IServiceConfiguration
{
    public static string ConfigurationPath => "AppSettings:Test";

    public string Option1 { get; set; } = "test123";
}
```

**`TestServiceWithOptions.cs`**
```csharp
[ServiceWithOptions<ServiceTestOptions>]
public class TestServiceWithOptions
{
    private readonly ServiceTestOptions _serviceConfiguration;

    public TestServiceWithOptions(IOptions<ServiceTestOptions> options)
    {
        _serviceConfiguration = options.Value;
    }

    public string GetOption1Value() => _serviceConfiguration.Option1;
}
```

## `[HostedService]`
Use this attribute to register hosted services with `IHostedService` forwarding behavior:
- Equivalent to `services.AddHostedService(sp => sp.GetRequiredService<TImplementation>())`.
- If the implementation is already registered as singleton, the hosted registration reuses that singleton instance.

### Parameters:
- **ImplementationType** (optional): hosted service concrete type. If omitted, the decorated class is used.
- **Lifetime** (optional): defaults to `Singleton`.

### Example
```csharp
[HostedService<MyBackgroundService>]
public class MyBackgroundServiceRegistrationMarker;
```

or directly on the hosted service:
```csharp
[HostedService]
public class MyBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
```

---

## `[ImplementationInstanceFor<TService, TProvider>]`
Use this attribute to register singleton implementation instances in a framework-like way (no `typeof(factory), nameof(method)` pairs).

### Provider contract
`TProvider` must implement:
```csharp
public interface IImplementationInstanceProvider<out TService>
{
    static abstract TService GetImplementationInstance();
}
```

### Parameters:
- **TService**: service type to register.
- **TProvider**: provider type that exposes `GetImplementationInstance`.
- **Key** (optional): keyed singleton registration key.

Feature-flag variant is also available:
- `[ImplementationInstanceFor<TService, TProvider, TFeatureFlag>]`

### Example (`Channel<T>`)
```csharp
public sealed class WorkItemChannelProvider
    : IImplementationInstanceProvider<Channel<MyHostedService.WorkItem>>
{
    public static Channel<MyHostedService.WorkItem> GetImplementationInstance()
        => Channel.CreateUnbounded<MyHostedService.WorkItem>();
}

[ImplementationInstanceFor<Channel<MyHostedService.WorkItem>, WorkItemChannelProvider>]
public class WorkItemChannelRegistrationMarker;
```

---

## `[Decorator]`
Use this attribute to automatically register decorators for specific services.

### Parameters:
- **ServiceType**: Specifies the service type to decorate. If `null`, the service type is automatically resolved from the first interface the class implements.
- **DecorationOrder**: Defines the order of decoration. Lower values are closer to the original implementation in the execution chain.
- **Key** (`.NET 8+`): Specifies a key for keyed decorator registration.
- **Feature** (optional): Specifies a feature flag to conditionally register the decorator.
- **TreatOpenGenericsAsWildcard** (optional, `bool`): When set to `true`, the decorator will treat open generic service types as a wildcard, allowing it to decorate any closed generic implementation of that service.

---

# Examples

## Basic Usage

### IService resolves to:
- `DecoratorA`
  - Wrapping `SampleService`

```csharp
[Service]
class SampleService : IService {}

[Decorator]
class DecoratorA : IService {}
```
### IService resolves to:
- `DecoratorB`
    - `Wrapping DecoratorA`
        - `Wrapping SampleService`

```csharp
[Service(serviceType: typeof(IService), lifetime: Lifetime.Singleton)]
class SampleService : IService {}

[Decorator(serviceType: typeof(IService), decorationOrder: 1)]
class DecoratorA : IService 
{
    public DecoratorA(IService service)
    {
        // `service` here is actually `SampleService`
    }
}

[Decorator(serviceType: typeof(IService), decorationOrder: 2)]
class DecoratorB : IService 
{
    public DecoratorB(IService service)
    {
        // `service` here is actually `DecoratorA`
    }
}
```

## .NET 8 Keyed Services

```csharp
[Service(Key = "randomKey")]
class SampleServiceDefault : IService {}

[Service(Key = "testKey")]
class SampleService : IService {}

[Decorator(Key = "testKey")]
class DecoratorA : IService {}

public class Test
{
    public Test(
        [FromKeyedServices("randomKey")] IService randomSvc,
        [FromKeyedServices("testKey")] IService svc)
    {
        // `randomSvc` resolves to `SampleServiceDefault`
        // `svc` resolves to `DecoratorA` wrapping `SampleService`
    }
}
```

## Triple-Attribute Hosted Service Pattern

Use all 3 attributes on one class to mimic:
- `AddSingleton<IContentRefreshWorkScheduler, ContentRefreshBatchHostedService>()`
- `AddSingleton(Channel.CreateUnbounded<WorkItem>())`
- `AddHostedService(sp => sp.GetRequiredService<ContentRefreshBatchHostedService>())`

```csharp
[Service(Lifetime = ServiceLifetime.Singleton, ServiceType = typeof(IContentRefreshWorkScheduler))]
[ImplementationInstanceFor<
    Channel<ContentRefreshBatchHostedService.WorkItem>,
    ContentRefreshBatchHostedService>]
[HostedService]
internal sealed class ContentRefreshBatchHostedService(
    Channel<ContentRefreshBatchHostedService.WorkItem> channel)
    : BackgroundService,
      IContentRefreshWorkScheduler,
      IImplementationInstanceProvider<Channel<ContentRefreshBatchHostedService.WorkItem>>
{
    public sealed class WorkItem;

    public static Channel<WorkItem> GetImplementationInstance()
        => Channel.CreateUnbounded<WorkItem>();

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
```

## Feature Flags
> **Note:** Starting from version 2.0.0, you can conditionally register services and decorators based on feature flags.

Feature-flag support in this library works as follows:
- Feature enums used with `[Service<TFeatureFlag>]` / `[Decorator<TFeatureFlag>]` must be marked with `[Flags]`.
- You can enable **multiple feature enum types** in the same registration call.
- `AddFeature(...)` merges repeated calls for the **same enum type** using bitwise OR.
- If an attribute declares multiple flags (e.g., `FeatureA | FeatureC`), registration is enabled when **at least one** of those flags is active.
- Use `FeatureMatchMode = FeatureMatchMode.Inactive` to register a service, decorator, or supported attribute when the specified feature is inactive.

```csharp
[Flags]
public enum SearchOptions
{
    None = 0,
    Azure = 1 << 0,
    Elastic = 1 << 1
}

[Flags]
public enum AnotherOptionsEnum
{
    None = 0,
    FeatureX = 1 << 0,
    FeatureY = 1 << 1
}

[Service<SearchOptions>(Feature = SearchOptions.Azure)]
class AzureSearchService : ISearchService {}

[Service<AnotherOptionsEnum>(Feature = AnotherOptionsEnum.FeatureX)]
class FeatureXService : IFeatureService {}
```

Enable flags in code:
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceAttributeBasedDependencyInjection(options =>
{
    options.AddFeature(SearchOptions.Azure);
    options.AddFeature(AnotherOptionsEnum.FeatureX);

    // same enum type is merged
    options.AddFeature(SearchOptions.Elastic);
});
```

Enable flags from appsettings (including multiple enum types):
```json
{
  "DIFeatureFlags": {
    "SearchOptions": ["Azure"],
    "AnotherOptionsEnum": ["FeatureX"]
  }
}
```

```csharp
builder.AddServiceAttributeBasedDependencyInjection(options =>
{
    options.SetFeaturesFromConfig(new Dictionary<string, Type>
    {
        { nameof(SearchOptions), typeof(SearchOptions) },
        { nameof(AnotherOptionsEnum), typeof(AnotherOptionsEnum) }
    });
});

// custom root section (instead of "DIFeatureFlags")
builder.AddServiceAttributeBasedDependencyInjection(options =>
{
    options.SetFeaturesFromConfig(
        new Dictionary<string, Type>
        {
            { nameof(SearchOptions), typeof(SearchOptions) },
            { nameof(AnotherOptionsEnum), typeof(AnotherOptionsEnum) }
        },
        "CustomFeatureFlagsRoot");
});
```

## Migration to Version 2.0.0

Version 2.0.0 originally raised the minimum to .NET 8. The current build targets .NET 10 and .NET 11; use a compatible older package release if your application still targets .NET 8 or .NET 9.
