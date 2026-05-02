using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IL.AttributeBasedDI.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class HostedServiceAttribute : Attribute
{
    public HostedServiceAttribute(Type? implementationType = null, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ImplementationType = implementationType;
        Lifetime = lifetime;
    }

    public Type? ImplementationType { get; init; }

    public ServiceLifetime Lifetime { get; init; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class HostedServiceAttribute<TImplementation> : HostedServiceAttribute
    where TImplementation : class, IHostedService
{
    public HostedServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        : base(typeof(TImplementation), lifetime)
    {
    }
}
