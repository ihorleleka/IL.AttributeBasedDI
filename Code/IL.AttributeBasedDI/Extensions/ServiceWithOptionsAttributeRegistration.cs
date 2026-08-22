using System.Reflection;
using System.Runtime.InteropServices;
using IL.AttributeBasedDI.Attributes;
using IL.AttributeBasedDI.Helpers;
using IL.AttributeBasedDI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IL.AttributeBasedDI.Extensions;

internal static class ServiceWithOptionsAttributeRegistration
{
    public static void RegisterClassesWithServiceAttributesWithOptions<TFeatureFlag>(this IServiceCollection serviceCollection,
        DiRegistrationSummary diRegistrationSummary,
        TFeatureFlag activeFeatures,
        IConfiguration? configuration = null,
        params Type[] types) where TFeatureFlag : struct, Enum
    {
        var serviceRegistrations = types
            .Where(type => type.GetCustomAttributes(typeof(ServiceWithOptionsAttribute<,>)).Any())
            .SelectMany(type =>
            {
                return type
                    .GetCustomAttributes(typeof(ServiceWithOptionsAttribute<,>))
                    .Select(attribute =>
                    {
                        var @base = attribute as ServiceAttribute<TFeatureFlag>;
                        if (@base != null)
                        {
                            RegisterOptionsFromAttribute(serviceCollection, configuration, attribute);
                        }

                        return @base?.ToRegistrationEntry(type);
                    });
            })
            .Where(x => x != null && FeatureFlagHelper.IsFeatureEnabled(activeFeatures, x.Feature, x.FeatureMatchMode))
            .ToList();

        foreach (var serviceRegistrationEntry in CollectionsMarshal.AsSpan(serviceRegistrations))
        {
            if (serviceRegistrationEntry!.ServiceType == null)
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

    private static readonly MethodInfo ConfigureMethod = typeof(OptionsConfigurationServiceCollectionExtensions)
        .GetMethods()
        .Where(x => x.Name == nameof(OptionsConfigurationServiceCollectionExtensions.Configure))
        .First(x =>
            {
                var parameters = x.GetParameters();
                return parameters.Length == 2
                       && parameters[0].ParameterType == typeof(IServiceCollection)
                       && parameters[1].ParameterType == typeof(IConfiguration);
            }
        );

    private static void RegisterOptionsFromAttribute(IServiceCollection serviceCollection, IConfiguration? configuration, Attribute attribute)
    {
        var configurationPathBase = attribute as IAttributeWithOptionsConfigurationPath;
        var configurationPath = configurationPathBase!.ConfigurationPath ?? string.Empty;
        var genericTypeUsedOnAttributeDeclaration = attribute.GetType().GetGenericArguments().First();
        var configurationSection = configuration?.GetSection(configurationPath);

        if (!string.IsNullOrEmpty(configurationPath) && configurationSection != null)
        {
            ConfigureMethod
                .MakeGenericMethod(genericTypeUsedOnAttributeDeclaration)
                .Invoke(null, [serviceCollection, configurationSection]);
        }
    }
}