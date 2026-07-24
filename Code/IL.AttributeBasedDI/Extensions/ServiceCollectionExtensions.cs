using IL.AttributeBasedDI.Helpers;
using IL.AttributeBasedDI.Models;
using IL.AttributeBasedDI.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IL.AttributeBasedDI.Extensions;

public static class ServiceCollectionExtensions
{
    public static DiRegistrationSummary AddServiceAttributeBasedDependencyInjection(this IServiceCollection serviceCollection,
        IConfiguration configuration,
        params string[] assemblyFilters)
    {
        return serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration, null, assemblyFilters);
    }

    public static DiRegistrationSummary AddServiceAttributeBasedDependencyInjection(this IServiceCollection serviceCollection,
        IConfiguration configuration,
        Action<FeatureBasedDIOptions>? configureOptions = null,
        params string[] assemblyFilters)
    {
        PreventEmptyAssemblyFilters(ref assemblyFilters);
        var registrationResult = new DiRegistrationSummary(serviceCollection);
        var options = new FeatureBasedDIOptions(configuration);
        configureOptions?.Invoke(options);
        serviceCollection.AddSingleton(Microsoft.Extensions.Options.Options.Create(options.ActiveFeatures));
        serviceCollection.AddSingleton(options);

        var methodInfo = typeof(ServiceRegistrationHelper)
            .GetMethod(nameof(ServiceRegistrationHelper.RegisterClassesWithServiceAttributeAndDecorators));

        foreach (var filter in assemblyFilters)
        {
            foreach (var featureEnum in options.ActiveFeatures.AllFeatures)
            {
                var enumType = featureEnum.GetType();

                var genericMethod = methodInfo!.MakeGenericMethod(enumType);

                genericMethod.Invoke(null,
                    [
                        serviceCollection,
                        registrationResult,
                        featureEnum,
                        configuration,
                        options.ThrowWhenDecorationTypeNotFound,
                        new[] { filter }
                    ]
                );
            }
        }
        
        return registrationResult;
    }

    private static void PreventEmptyAssemblyFilters(ref string[] assemblyFilters)
    {
        if (assemblyFilters.Length == 0)
        {
            assemblyFilters = ["*"];
        }
    }
}