using System.Runtime.InteropServices;
using IL.AttributeBasedDI.Attributes;
using IL.AttributeBasedDI.Helpers;
using IL.AttributeBasedDI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace IL.AttributeBasedDI.Extensions;

internal static class ImplementationInstanceAttributeRegistration
{
    public static void RegisterImplementationInstances<TFeatureFlag>(this IServiceCollection serviceCollection,
        DiRegistrationSummary diRegistrationSummary,
        TFeatureFlag activeFeatures,
        params Type[] types)
        where TFeatureFlag : struct, Enum
    {
        var registrations = types
            .SelectMany(type => type.GetCustomAttributes(false).OfType<IImplementationInstanceRegistration<TFeatureFlag>>())
            .Where(attribute => FeatureFlagHelper.IsFeatureEnabled(activeFeatures, attribute.Feature))
            .ToList();

        foreach (var attribute in CollectionsMarshal.AsSpan(registrations))
        {
            var instance = attribute.CreateInstance();
            var serviceType = attribute.ServiceType;

            if (!serviceType.IsAssignableFrom(instance.GetType()))
            {
                throw new InvalidOperationException(
                    $"Created instance type '{instance.GetType().FullName}' is not assignable to '{serviceType.FullName}'.");
            }

            if (!string.IsNullOrWhiteSpace(attribute.Key))
            {
                serviceCollection.AddKeyedSingleton(serviceType, attribute.Key, instance);
            }
            else
            {
                serviceCollection.AddSingleton(serviceType, instance);
            }

            diRegistrationSummary.ServiceGraph.AddOrMerge(new RegistrationEntry<TFeatureFlag>
            {
                Lifetime = ServiceLifetime.Singleton,
                ServiceType = serviceType,
                ImplementationType = instance.GetType(),
                Key = attribute.Key,
                Feature = attribute.Feature
            });
        }
    }
}
