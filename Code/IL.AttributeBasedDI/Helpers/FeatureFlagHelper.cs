using IL.AttributeBasedDI.Models;

namespace IL.AttributeBasedDI.Helpers;

public static class FeatureFlagHelper
{
    public static bool IsFeatureEnabled<TFeatureFlag>(TFeatureFlag activeFeatures, TFeatureFlag feature, FeatureMatchMode matchMode = FeatureMatchMode.Active) where TFeatureFlag : struct, Enum
    {
        if (feature is FeaturesNoop)
            return matchMode == FeatureMatchMode.Active;

        var active = Convert.ToInt32(activeFeatures);
        var target = Convert.ToInt32(feature);
        var isActive = (active & target) != 0;
        return matchMode == FeatureMatchMode.Active ? isActive : !isActive;
    }
}