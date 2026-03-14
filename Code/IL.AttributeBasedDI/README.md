# IL.AttributeBasedDI
Control dependencies and decorators via custom attributes - extends Microsoft.Extensions.DependencyInjection.

> **Note:** Starting from version **2.0.0**, only **.NET 8 or higher** is supported.

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

## Feature Flags
> **Note:** Starting from version 2.0.0, you can conditionally register services and decorators based on feature flags.

Feature-flag support in this library works as follows:
- Feature enums used with `[Service<TFeatureFlag>]` / `[Decorator<TFeatureFlag>]` must be marked with `[Flags]`.
- You can enable **multiple feature enum types** in the same registration call.
- `AddFeature(...)` merges repeated calls for the **same enum type** using bitwise OR.
- If an attribute declares multiple flags (e.g., `FeatureA | FeatureC`), registration is enabled when **at least one** of those flags is active.

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

Starting from version 2.0.0, only .NET 8 or higher is supported. If you're upgrading from an earlier version, ensure your project targets .NET 8 or higher.