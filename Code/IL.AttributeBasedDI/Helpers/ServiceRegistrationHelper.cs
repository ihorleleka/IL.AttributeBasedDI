using System.Reflection;
using IL.AttributeBasedDI.Attributes;
using IL.AttributeBasedDI.Extensions;
using IL.AttributeBasedDI.Models;
using IL.Misc.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IL.AttributeBasedDI.Helpers;

internal static class ServiceRegistrationHelper
{
    public static void RegisterClassesWithServiceAttributeAndDecorators<TFeatureFlag>(this IServiceCollection serviceCollection,
        DiRegistrationSummary diRegistrationSummary,
        TFeatureFlag activeFeatures,
        IConfiguration configuration,
        bool throwWhenDecorationTypeNotFound,
        params string[] assemblyFilters) where TFeatureFlag : struct, Enum
    {
        var assemblies = TypesAndAssembliesHelper.GetAssemblies(assemblyFilters);
        var allTypes = GetAllTypesFromAssemblies(assemblies);
        serviceCollection.RegisterClassesWithServiceAttributes(diRegistrationSummary, activeFeatures, allTypes);
        serviceCollection.RegisterImplementationInstances(diRegistrationSummary, activeFeatures, allTypes);
        serviceCollection.RegisterHostedServices(diRegistrationSummary, activeFeatures, allTypes);
        serviceCollection.RegisterClassesWithServiceAttributesWithOptions(diRegistrationSummary, activeFeatures, configuration, allTypes);
        serviceCollection.RegisterClassesWithDecoratorAttributes(diRegistrationSummary, activeFeatures, throwWhenDecorationTypeNotFound, allTypes);
    }

    public static Type? GetServiceTypeBasedOnDependencyInjectionAttribute<TFeatureFlag>(Type sourceType,
        DependencyInjectionAttributeBase<TFeatureFlag> dependencyInjectionAttributeBase,
        bool fallbackToBaseTypeOrSelf = false)
        where TFeatureFlag : struct, Enum
    {
        return dependencyInjectionAttributeBase.FindServiceTypeAutomatically
            ? ExtractServiceTypeFromSupertypes(sourceType, fallbackToBaseTypeOrSelf)
            : dependencyInjectionAttributeBase.ServiceType;
    }

    private static Type? ExtractServiceTypeFromSupertypes(Type sourceType, bool fallbackToBaseTypeOrSelf)
    {
        return sourceType.GetInterfaces().FirstOrDefault()
               ?? (fallbackToBaseTypeOrSelf ? sourceType.BaseType ?? sourceType : null);
    }

    private static Type[] GetAllTypesFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsAbstract: false })
            .ToArray();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }
}
