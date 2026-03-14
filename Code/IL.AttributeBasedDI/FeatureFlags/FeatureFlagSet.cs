using IL.AttributeBasedDI.Models;

namespace IL.AttributeBasedDI.FeatureFlags;

public sealed class FeatureFlagSet
{
    private readonly Dictionary<Type, Enum> _activeFeatures = new();

    public IReadOnlyCollection<Enum> AllFeatures => _activeFeatures.Values.ToList();

    internal FeatureFlagSet(params Enum[] features)
    {
        // Always add FeatureNoop
        AddOrMerge(FeaturesNoop.None);

        foreach (var feature in features)
        {
            AddOrMerge(feature);
        }
    }

    internal void AddOrMerge(Enum feature)
    {
        var type = feature.GetType();

        if (!type.IsEnum || !type.IsDefined(typeof(FlagsAttribute), false))
        {
            throw new ArgumentException($"Enum of type {type.Name} must have [Flags] attribute.");
        }

        if (_activeFeatures.TryGetValue(type, out var existing))
        {
            var mergedValue = Convert.ToInt64(existing) | Convert.ToInt64(feature);
            _activeFeatures[type] = (Enum)Enum.ToObject(type, mergedValue);
        }
        else
        {
            _activeFeatures[type] = feature;
        }
    }

    public bool IsFeatureActive<TFeatureFlag>(TFeatureFlag feature) where TFeatureFlag : struct, Enum =>
        _activeFeatures.TryGetValue(typeof(TFeatureFlag), out var flags)
        && flags.HasFlag(feature);
}
