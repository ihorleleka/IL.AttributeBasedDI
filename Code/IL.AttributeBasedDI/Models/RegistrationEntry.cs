using Microsoft.Extensions.DependencyInjection;

namespace IL.AttributeBasedDI.Models;

internal sealed class RegistrationEntry<TFeatureFlag> where TFeatureFlag : struct, Enum
{
    public ServiceLifetime Lifetime { get; init; }

    public Type? ServiceType { get; init; }

    public Type ImplementationType { get; init; } = null!;

    public string? Key { get; init; }

    public TFeatureFlag Feature { get; set; }

    public FeatureMatchMode FeatureMatchMode { get; set; } = FeatureMatchMode.Active;
}