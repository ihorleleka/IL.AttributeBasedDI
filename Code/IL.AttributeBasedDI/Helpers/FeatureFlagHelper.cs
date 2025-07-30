using IL.AttributeBasedDI.Models;

namespace IL.AttributeBasedDI.Helpers;

public static class FeatureFlagHelper
{
    public static bool IsFeatureEnabled<TFeatureFlag>(TFeatureFlag activeFeatures, TFeatureFlag feature) where TFeatureFlag : struct, Enum
    {
        if (feature is FeaturesNoop)
            return true;

        var active = Convert.ToInt32(activeFeatures);
        var target = Convert.ToInt32(feature);
        return (active & target) != 0;
    }
}