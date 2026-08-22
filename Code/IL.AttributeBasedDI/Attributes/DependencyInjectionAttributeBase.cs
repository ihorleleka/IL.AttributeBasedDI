using IL.AttributeBasedDI.Models;

namespace IL.AttributeBasedDI.Attributes;

public abstract class DependencyInjectionAttributeBase<TFeatureFlag>(TFeatureFlag feature) : Attribute where TFeatureFlag : struct, Enum
{
    public Type? ServiceType { get; init; }

    public bool FindServiceTypeAutomatically => ServiceType is null;

    public TFeatureFlag Feature { get; init; } = feature;

    /// <summary>
    /// Determines how <see cref="Feature"/> is matched against active features.
    /// </summary>
    public FeatureMatchMode FeatureMatchMode { get; init; } = FeatureMatchMode.Active;
}