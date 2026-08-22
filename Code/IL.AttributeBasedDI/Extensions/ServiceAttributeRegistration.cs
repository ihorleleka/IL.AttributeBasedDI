using System.Reflection;
using System.Runtime.InteropServices;
using IL.AttributeBasedDI.Attributes;
using IL.AttributeBasedDI.Helpers;
using IL.AttributeBasedDI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace IL.AttributeBasedDI.Extensions;

internal static class ServiceAttributeRegistration
{
    public static void RegisterClassesWithServiceAttributes<TFeatureFlag>(this IServiceCollection serviceCollection,
        DiRegistrationSummary diRegistrationSummary, 
        TFeatureFlag activeFeatures, 
        params Type[] types)
        where TFeatureFlag : struct, Enum
    {
        var serviceRegistrations = types
            .Where(type => type.GetCustomAttributes(typeof(ServiceAttribute<TFeatureFlag>)).Any())
            .SelectMany(type =>
                type
                    .GetCustomAttributes<ServiceAttribute<TFeatureFlag>>()
                    .Select(attribute => ToRegistrationEntry(attribute, type))
            )
            .Where(x => FeatureFlagHelper.IsFeatureEnabled(activeFeatures, x.Feature, x.FeatureMatchMode))
            .ToList();

        foreach (var serviceRegistrationEntry in CollectionsMarshal.AsSpan(serviceRegistrations))
        {
            if (serviceRegistrationEntry.ServiceType == null)
            {
                serviceCollection.AddServiceWithLifetime(serviceRegistrationEntry.ImplementationType,
                    null,
                    serviceRegistrationEntry.Lifetime,
                    serviceRegistrationEntry.Key);
            }
            else
            {
                serviceCollection.AddServiceWithLifetime(serviceRegistrationEntry.ServiceType,
                    serviceRegistrationEntry.ImplementationType,
                    serviceRegistrationEntry.Lifetime,
                    serviceRegistrationEntry.Key);
            }
            
            diRegistrationSummary.ServiceGraph.AddOrMerge(serviceRegistrationEntry);
        }
    }

    public static RegistrationEntry<TFeatureFlag> ToRegistrationEntry<TFeatureFlag>(this ServiceAttribute<TFeatureFlag> attribute, Type type)
        where TFeatureFlag : struct, Enum
    {
        return new RegistrationEntry<TFeatureFlag>
        {
            Key = attribute.Key,
            Lifetime = attribute.Lifetime,
            ServiceType = ServiceRegistrationHelper.GetServiceTypeBasedOnDependencyInjectionAttribute(type, attribute),
            ImplementationType = type,
            Feature = attribute.Feature,
            FeatureMatchMode = attribute.FeatureMatchMode
        };
    }

    public static void AddServiceWithLifetime(
        this IServiceCollection serviceCollection,
        Type serviceType,
        Type? implementationType,
        ServiceLifetime lifetime,
        string? key)
    {
        implementationType ??= serviceType;

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                if (!string.IsNullOrEmpty(key))
                {
                    serviceCollection.AddKeyedSingleton(serviceType, key, implementationType);
                }
                else
                {
                    serviceCollection.AddSingleton(serviceType, implementationType);
                }

                break;

            case ServiceLifetime.Transient:
                if (!string.IsNullOrEmpty(key))
                {
                    serviceCollection.AddKeyedTransient(serviceType, key, implementationType);
                }
                else
                {
                    serviceCollection.AddTransient(serviceType, implementationType);
                }

                break;

            case ServiceLifetime.Scoped:
                if (!string.IsNullOrEmpty(key))
                {
                    serviceCollection.AddKeyedScoped(serviceType, key, implementationType);
                }
                else
                {
                    serviceCollection.AddScoped(serviceType, implementationType);
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    }
}