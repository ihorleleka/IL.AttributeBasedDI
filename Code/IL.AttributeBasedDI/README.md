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

```csharp

[Flags]
public enum Features
{
    None = 0,
    FeatureA = 1 << 0,
    FeatureB = 1 << 1,
    FeatureC = 1 << 2
}

[Service<Features>(Feature = Features.FeatureA)]
class FeatureAService : IService {}

[Service<Features>(Feature = Features.FeatureB)]
class FeatureBService : IService {}

[Decorator<Features>(Feature = Features.FeatureA)]
class FeatureADecorator : IService {}

[Decorator<Features>(Feature = Features.FeatureB)]
class FeatureBDecorator : IService {}

```

```csharp

var builder = WebApplication.CreateBuilder(args);

// single feature type
builder.AddServiceAttributeBasedDependencyInjection(configuration,
    options => options.AddFeature(Features.FeatureA | Features.FeatureC)
);

//or multiple feature types
builder.AddServiceAttributeBasedDependencyInjection(configuration,
    options => {
        options.AddFeature(Features.FeatureA | Features.FeatureC);
        options.AddFeature(AnotherFeaturesEnum.FeatureA | AnotherFeaturesEnum.FeatureC);
    }
);

// AddFeature will merge features of same type if called multiple times resulting same as bit operator invariant (FeatureA | FeatureC)
builder.AddServiceAttributeBasedDependencyInjection(configuration,
    options => {
        options.AddFeature(Features.FeatureA);
        options.AddFeature(Features.FeatureC);
        options.AddFeature(AnotherFeaturesEnum.FeatureA);
        options.AddFeature(AnotherFeaturesEnum.FeatureC);
    }
);

```
### or appsettings.json based:
```json
{
    "DIFeatureFlags": {
        "Features": ["FeatureA", "FeatureC"]
    }
}
```
### and then you have to specify key/type mappings in options:
```csharp

builder.AddServiceAttributeBasedDependencyInjection(configuration,
            options => options.SetFeaturesFromConfig(
                new Dictionary<string, Type>
                {
                    { nameof(Features), typeof(Features) }
                }
            );
        );

// you can also specify custom root object key in appsettings.json instead of "DIFeatureFlags"
builder.AddServiceAttributeBasedDependencyInjection(configuration,
            options => options.SetFeaturesFromConfig(
                new Dictionary<string, Type>
                {
                    { nameof(Features), typeof(Features) }
                },
                "CustomRootObjectKeyInsteadOfDIFeatureFlags"
            );
        );

```

## Migration to Version 2.0.0

Starting from version 2.0.0, only .NET 8 or higher is supported. If you're upgrading from an earlier version, ensure your project targets .NET 8 or higher.