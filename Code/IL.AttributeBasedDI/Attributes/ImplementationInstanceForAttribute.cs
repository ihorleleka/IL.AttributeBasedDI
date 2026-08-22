using IL.AttributeBasedDI.Models;

namespace IL.AttributeBasedDI.Attributes;

public interface IImplementationInstanceRegistration<TFeatureFlag> where TFeatureFlag : struct, Enum
{
    Type ServiceType { get; }

    string? Key { get; }

    TFeatureFlag Feature { get; }

    FeatureMatchMode FeatureMatchMode { get; }

    object CreateInstance();
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class ImplementationInstanceForAttribute<TService, TProvider> :
    ImplementationInstanceForAttribute<TService, TProvider, FeaturesNoop>
    where TProvider : IImplementationInstanceProvider<TService>
{
    public ImplementationInstanceForAttribute(string? key = null) : base(key)
    {
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class ImplementationInstanceForAttribute<TService, TProvider, TFeatureFlag> :
    DependencyInjectionAttributeBase<TFeatureFlag>,
    IImplementationInstanceRegistration<TFeatureFlag>
    where TProvider : IImplementationInstanceProvider<TService>
    where TFeatureFlag : struct, Enum
{
    public ImplementationInstanceForAttribute(string? key = null, TFeatureFlag feature = default) : base(feature)
    {
        Key = key;
    }

    public new Type ServiceType => typeof(TService);

    public string? Key { get; init; }

    public object CreateInstance()
    {
        var instance = TProvider.GetImplementationInstance();
        if (instance is null)
        {
            throw new InvalidOperationException(
                $"Provider '{typeof(TProvider).FullName}' returned null implementation instance.");
        }

        return instance;
    }
}
