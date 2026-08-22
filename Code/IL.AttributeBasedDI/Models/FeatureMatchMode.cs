namespace IL.AttributeBasedDI.Models;

/// <summary>
/// Determines how an attribute's feature flag is matched against active features.
/// </summary>
public enum FeatureMatchMode
{
    /// <summary>Register when the feature is active.</summary>
    Active,

    /// <summary>Register when the feature is inactive.</summary>
    Inactive,

}
