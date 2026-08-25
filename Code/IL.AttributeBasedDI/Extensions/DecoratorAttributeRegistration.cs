using System.Reflection;
using IL.AttributeBasedDI.Attributes;
using IL.AttributeBasedDI.Exceptions;
using IL.AttributeBasedDI.Helpers;
using IL.AttributeBasedDI.Models;
using IL.Misc.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace IL.AttributeBasedDI.Extensions;

internal static class DecoratorAttributeRegistration
{
    private const string WildcardKey = "*";
    
    private sealed record DecorationRegistrationEntry<TFeatureFlag>(
        string? Key,
        int DecorationOrder,
        TFeatureFlag Feature,
        FeatureMatchMode FeatureMatchMode,
        Type? ServiceType,
        Type DecoratorImplementationType,
        bool TreatOpenGenericsAsWildcard);

    public static void RegisterClassesWithDecoratorAttributes<TFeatureFlag>(this IServiceCollection serviceCollection,
        DiRegistrationSummary diRegistrationSummary,
        TFeatureFlag activeFeatures,
        bool throwWhenDecorationTypeNotFound,
        params Type[] types)
        where TFeatureFlag : struct, Enum
    {
        var serviceDecorations = types
            .Where(type => type.GetCustomAttribute<DecoratorAttribute<TFeatureFlag>>() != null)
            .Select(type =>
            {
                var decoratorAttribute = type.GetCustomAttribute<DecoratorAttribute<TFeatureFlag>>();
                return new DecorationRegistrationEntry<TFeatureFlag>(
                    decoratorAttribute!.Key,
                    decoratorAttribute.DecorationOrder,
                    decoratorAttribute.Feature,
                    decoratorAttribute.FeatureMatchMode,
                    ServiceRegistrationHelper.GetServiceTypeBasedOnDependencyInjectionAttribute(type, decoratorAttribute, true),
                    type,
                    decoratorAttribute.TreatOpenGenericsAsWildcard);
            })
            .Where(x => FeatureFlagHelper.IsFeatureEnabled(activeFeatures, x.Feature, x.FeatureMatchMode))
            .OrderBy(x => x.DecorationOrder)
            .ToList();

        ValidateDecoratorChainDoesNotContainDuplicates(serviceDecorations);

        foreach (var serviceDecorationEntry in serviceDecorations)
        {
            if (serviceDecorationEntry.ServiceType == null)
            {
                if (!throwWhenDecorationTypeNotFound)
                {
                    continue;
                }

                throw new ServiceDecorationException($"Can't determine service to decorate. Decorator type: {serviceDecorationEntry.DecoratorImplementationType.FullName}");
            }

            if (IsSelfDecoration(serviceDecorationEntry.ServiceType, serviceDecorationEntry.DecoratorImplementationType))
            {
                throw new ServiceDecorationException(
                    $"Self-decoration is not supported. Type '{serviceDecorationEntry.DecoratorImplementationType.FullName}' cannot decorate itself.");
            }

            serviceCollection.AddDecoratorForService(serviceDecorationEntry.ServiceType,
                serviceDecorationEntry.DecoratorImplementationType,
                serviceDecorationEntry.Key,
                serviceDecorationEntry.TreatOpenGenericsAsWildcard,
                throwWhenDecorationTypeNotFound);
            diRegistrationSummary.ServiceGraph.AddDecorator(serviceDecorationEntry.ServiceType,
                serviceDecorationEntry.DecoratorImplementationType,
                serviceDecorationEntry.Key,
                serviceDecorationEntry.Feature,
                serviceDecorationEntry.TreatOpenGenericsAsWildcard);
        }
    }

    //Credits to https://greatrexpectations.com/2018/10/25/decorators-in-net-core-with-dependency-injection
    private static void AddDecoratorForService(this IServiceCollection serviceCollection,
        Type serviceType,
        Type decoratorImplementationType,
        string? key,
        bool treatOpenGenericsAsWildcard,
        bool throwWhenDecorationTypeNotFound)
    {
        if (!serviceType.IsGenericType || !decoratorImplementationType.IsGenericType)
        {
            HandleNonGenericDecorators(serviceCollection,
                serviceType,
                decoratorImplementationType,
                key,
                throwWhenDecorationTypeNotFound);
        }
        else if (treatOpenGenericsAsWildcard
                 && serviceType.IsGenericType
                 && decoratorImplementationType.ContainsGenericParameters)
        {
            HandleGenericDecoratorsWithTreatOpenGenericsAsWildcard(serviceCollection,
                serviceType,
                decoratorImplementationType,
                key,
                throwWhenDecorationTypeNotFound);
        }
        else
        {
            // standard open generics are not supported for now
        }
    }

    private static void HandleGenericDecoratorsWithTreatOpenGenericsAsWildcard(IServiceCollection serviceCollection,
        Type serviceType,
        Type decoratorImplementationType,
        string? key,
        bool throwWhenDecorationTypeNotFound)
    {
        var descriptorsToDecorate = serviceCollection
            .Select((descriptor, index) => new { Descriptor = descriptor, Index = index })
            .Where(entry =>
            {
                var valid = entry.Descriptor.ServiceType.FullName?.StartsWith(serviceType.FullName ?? string.Empty) is true;
                if (valid && !string.IsNullOrEmpty(key))
                {
                    throw new ServiceDecorationException("Wildcard open generics decoration for keyed services is not supported!");
                }

                return valid;
            })
            .ToList();
        if (descriptorsToDecorate.Count == 0)
        {
            if (!throwWhenDecorationTypeNotFound)
            {
                return;
            }

            throw new ServiceDecorationException($"No services registered for type {serviceType.FullName} in ServiceCollection, Decoration is impossible.");
        }

        foreach (var entry in descriptorsToDecorate)
        {
            var descriptor = entry.Descriptor;
            var genericArguments = descriptor.ServiceType.GetGenericArguments();
            if (genericArguments.Any(x => x.ContainsGenericParameters))
            {
                // standard open generics are not supported for treatOpenGenericsAsWildcard = true
                continue;
            }

            var closedDecoratorType = decoratorImplementationType.MakeGenericType(genericArguments);
            serviceCollection[entry.Index] = ServiceDescriptor.Describe(
                descriptor.ServiceType,
                provider => CreateDecorator(provider, descriptor, closedDecoratorType),
                descriptor.Lifetime);
        }
    }

    private static void HandleNonGenericDecorators(IServiceCollection serviceCollection,
        Type serviceType,
        Type decoratorImplementationType,
        string? key,
        bool throwWhenDecorationTypeNotFound)
    {
        var descriptorsToDecorate = serviceCollection
            .Select((descriptor, index) => new { Descriptor = descriptor, Index = index })
            .Where(entry => IsMatchingDescriptor(entry.Descriptor, serviceType, key))
            .ToList();

        if (descriptorsToDecorate.Count == 0)
        {
            if (!throwWhenDecorationTypeNotFound)
            {
                return;
            }

            throw new ServiceDecorationException($"No services registered for type {serviceType.FullName} in ServiceCollection, Decoration is impossible.");
        }

        foreach (var entry in descriptorsToDecorate)
        {
            var descriptor = entry.Descriptor;
            var decoratorDescriptor = descriptor.IsKeyedService
                ? ServiceDescriptor.DescribeKeyed(
                    descriptor.ServiceType,
                    descriptor.ServiceKey,
                    (serviceProvider, _) => CreateDecorator(serviceProvider, descriptor, decoratorImplementationType),
                    descriptor.Lifetime
                )
                : ServiceDescriptor.Describe(
                    descriptor.ServiceType,
                    serviceProvider => CreateDecorator(serviceProvider, descriptor, decoratorImplementationType),
                    descriptor.Lifetime);
            serviceCollection[entry.Index] = decoratorDescriptor;
        }
    }

    private static bool IsMatchingDescriptor(ServiceDescriptor descriptor, Type serviceType, string? key)
    {
        if (descriptor.ServiceType != serviceType)
        {
            return false;
        }

        if (string.IsNullOrEmpty(key))
        {
            return !descriptor.IsKeyedService;
        }

        if (!descriptor.IsKeyedService)
        {
            return false;
        }

        var descriptorKey = descriptor.ServiceKey?.ToString();
        return descriptorKey == key
               || descriptorKey is not null && IsWildcardKey(key) && descriptorKey.MatchesWildcard(key);
    }

    private static object CreateDecorator(IServiceProvider provider, ServiceDescriptor originalDescriptor, Type decoratorImplementationType)
    {
        var inner = CreateOriginalInstance(provider, originalDescriptor);
        return DecoratorAcceptsInnerInstance(decoratorImplementationType, inner)
            ? ActivatorUtilities.CreateInstance(provider, decoratorImplementationType, inner)
            : ActivatorUtilities.CreateInstance(provider, decoratorImplementationType);
    }

    private static bool DecoratorAcceptsInnerInstance(Type decoratorImplementationType, object inner)
    {
        return decoratorImplementationType
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType.IsInstanceOfType(inner));
    }

    private static object CreateOriginalInstance(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.IsKeyedService)
        {
            if (descriptor.KeyedImplementationInstance is not null)
            {
                return descriptor.KeyedImplementationInstance;
            }

            if (descriptor.KeyedImplementationFactory is not null)
            {
                return descriptor.KeyedImplementationFactory(provider, descriptor.ServiceKey);
            }

            if (descriptor.KeyedImplementationType is not null)
            {
                return ActivatorUtilities.CreateInstance(provider, descriptor.KeyedImplementationType);
            }

            throw new ServiceDecorationException($"Unable to create keyed service '{descriptor.ServiceType.FullName}'.");
        }

        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(provider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);
        }

        throw new ServiceDecorationException($"Unable to create service '{descriptor.ServiceType.FullName}'.");
    }

    private static bool IsSelfDecoration(Type serviceType, Type decoratorImplementationType)
    {
        if (serviceType == decoratorImplementationType)
        {
            return true;
        }

        if (!serviceType.IsGenericType || !decoratorImplementationType.IsGenericType)
        {
            return false;
        }

        return serviceType.GetGenericTypeDefinition() == decoratorImplementationType.GetGenericTypeDefinition();
    }

    private static void ValidateDecoratorChainDoesNotContainDuplicates<TFeatureFlag>(
        IReadOnlyList<DecorationRegistrationEntry<TFeatureFlag>> serviceDecorations)
        where TFeatureFlag : struct, Enum
    {
        for (var i = 0; i < serviceDecorations.Count; i++)
        {
            var current = serviceDecorations[i];
            for (var j = i + 1; j < serviceDecorations.Count; j++)
            {
                var next = serviceDecorations[j];
                if (current.DecoratorImplementationType != next.DecoratorImplementationType)
                {
                    continue;
                }

                if (current.ServiceType == null || next.ServiceType == null)
                {
                    continue;
                }

                if (!IsSameServiceTarget(current.ServiceType, next.ServiceType))
                {
                    continue;
                }

                if (!CanKeysOverlap(current.Key, next.Key))
                {
                    continue;
                }

                throw new ServiceDecorationException(
                    $"Decorator '{current.DecoratorImplementationType.FullName}' appears more than once in the same decoration chain for service '{current.ServiceType.FullName}'.");
            }
        }
    }

    private static bool IsSameServiceTarget(Type currentServiceType, Type nextServiceType)
    {
        if (currentServiceType == nextServiceType)
        {
            return true;
        }

        return currentServiceType.IsGenericType
               && nextServiceType.IsGenericType
               && currentServiceType.GetGenericTypeDefinition() == nextServiceType.GetGenericTypeDefinition();
    }

    private static bool CanKeysOverlap(string? keyA, string? keyB)
    {
        var isEmptyA = string.IsNullOrEmpty(keyA);
        var isEmptyB = string.IsNullOrEmpty(keyB);
        if (isEmptyA || isEmptyB)
        {
            return isEmptyA && isEmptyB;
        }

        if (keyA == keyB)
        {
            return true;
        }

        var nonNullKeyA = keyA!;
        var nonNullKeyB = keyB!;
        var keyAIsWildcard = IsWildcardKey(nonNullKeyA);
        var keyBIsWildcard = IsWildcardKey(nonNullKeyB);

        if (keyAIsWildcard && keyBIsWildcard)
        {
            return nonNullKeyA.MatchesWildcard(nonNullKeyB) || nonNullKeyB.MatchesWildcard(nonNullKeyA);
        }

        if (keyAIsWildcard)
        {
            return nonNullKeyB.MatchesWildcard(nonNullKeyA);
        }

        if (keyBIsWildcard)
        {
            return nonNullKeyA.MatchesWildcard(nonNullKeyB);
        }

        return false;
    }

    private static bool IsWildcardKey(string key)
    {
        return key.Contains(WildcardKey);
    }

}
