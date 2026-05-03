using System.Reflection;
using System.Runtime.InteropServices;
using IL.AttributeBasedDI.Attributes;
using IL.AttributeBasedDI.Exceptions;
using IL.AttributeBasedDI.Helpers;
using IL.AttributeBasedDI.Models;
using IL.Misc.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IL.AttributeBasedDI.Extensions;

internal static class DecoratorAttributeRegistration
{
    private const string WildcardKey = "*";
    
    private sealed record DecorationRegistrationEntry<TFeatureFlag>(
        string? Key,
        int DecorationOrder,
        TFeatureFlag Feature,
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
                    ServiceRegistrationHelper.GetServiceTypeBasedOnDependencyInjectionAttribute(type, decoratorAttribute, true),
                    type,
                    decoratorAttribute.TreatOpenGenericsAsWildcard);
            })
            .Where(x => FeatureFlagHelper.IsFeatureEnabled(activeFeatures, x.Feature))
            .OrderBy(x => x.DecorationOrder)
            .ToList();

        ValidateDecoratorChainDoesNotContainDuplicates(serviceDecorations);

        foreach (var serviceDecorationEntry in CollectionsMarshal.AsSpan(serviceDecorations))
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
            .Where(s =>
            {
                var valid = s.ServiceType.FullName?.StartsWith(serviceType.FullName ?? string.Empty) is true;
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

        foreach (var descriptor in CollectionsMarshal.AsSpan(descriptorsToDecorate))
        {
            var genericArguments = descriptor.ServiceType.GetGenericArguments();
            if (genericArguments.Any(x => x.ContainsGenericParameters))
            {
                // standard open generics are not supported for treatOpenGenericsAsWildcard = true
                continue;
            }

            var closedDecoratorType = decoratorImplementationType.MakeGenericType(genericArguments);
            var objectFactory = ActivatorUtilities.CreateFactory(closedDecoratorType, [descriptor.ServiceType]);
            serviceCollection.Replace(
                ServiceDescriptor.Describe(
                    descriptor.ServiceType,
                    implementationFactory => objectFactory(implementationFactory, [implementationFactory.CreateInstance(descriptor)]),
                    descriptor.Lifetime)
            );
        }
    }

    private static void HandleNonGenericDecorators(IServiceCollection serviceCollection,
        Type serviceType,
        Type decoratorImplementationType,
        string? key,
        bool throwWhenDecorationTypeNotFound)
    {
        var descriptorsToDecorate = serviceCollection
            .Where(s =>
            {
                var valid = s.ServiceType == serviceType;
                if (string.IsNullOrEmpty(key))
                {
                    return valid && !s.IsKeyedService;
                }

                var descriptorServiceKey = s.ServiceKey?.ToString();
                return descriptorServiceKey == key
                       || !string.IsNullOrEmpty(descriptorServiceKey) && IsWildcardKey(key) && descriptorServiceKey.MatchesWildcard(key);
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

        foreach (var descriptor in CollectionsMarshal.AsSpan(descriptorsToDecorate))
        {
            var decoratorDescriptor = !string.IsNullOrEmpty(key)
                ? ServiceDescriptor.DescribeKeyed(
                    serviceType,
                    IsWildcardKey(key) ? descriptor.ServiceKey!.ToString() : key,
                    (serviceProvider, _) => CreateDecoratorInstance(serviceType, decoratorImplementationType, descriptor, serviceProvider),
                    descriptor.Lifetime
                )
                : ServiceDescriptor.Describe(
                    serviceType,
                    serviceProvider => CreateDecoratorInstance(serviceType, decoratorImplementationType, descriptor, serviceProvider),
                    descriptor.Lifetime);
            serviceCollection.Replace(decoratorDescriptor);
        }
    }

    private static object CreateDecoratorInstance(Type serviceType, Type decoratorImplementationType, ServiceDescriptor descriptor, IServiceProvider serviceProvider)
    {
        var hasOriginalImplementationInOneOfContstructorsParameters = decoratorImplementationType
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == serviceType);

        object?[] args;
        Type[] paramTypes;

        if (IsServiceTypeAndImplementationMatchingDescriptorServiceType(serviceType, descriptor))
        {
            // Prevent infinite loop by passing null if decorator depends on the same service type.
            args = hasOriginalImplementationInOneOfContstructorsParameters ? [null] : [];
            paramTypes = hasOriginalImplementationInOneOfContstructorsParameters ? [serviceType] : Type.EmptyTypes;
        }
        else
        {
            args = hasOriginalImplementationInOneOfContstructorsParameters ? [serviceProvider.CreateInstance(descriptor)] : [];
            paramTypes = hasOriginalImplementationInOneOfContstructorsParameters ? [serviceType] : Type.EmptyTypes;
        }

        return ActivatorUtilities.CreateFactory(decoratorImplementationType, paramTypes)(serviceProvider, args);
    }

    /// <summary>
    /// If we are decorating without interfaces and reusing same type as service type - that will cause infinite loop.
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    private static bool IsServiceTypeAndImplementationMatchingDescriptorServiceType(Type serviceType, ServiceDescriptor descriptor)
    {
        return !serviceType.IsInterface
               && descriptor.ServiceType == serviceType
               && descriptor.ImplementationType == serviceType;
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

    private static object CreateInstance(this IServiceProvider serviceProvider, ServiceDescriptor serviceDescriptor) =>
        serviceDescriptor switch
        {
            { IsKeyedService: true, KeyedImplementationInstance: not null } => serviceDescriptor.KeyedImplementationInstance,
            { IsKeyedService: true, KeyedImplementationFactory: not null } => serviceDescriptor.KeyedImplementationFactory(serviceProvider, serviceDescriptor.ServiceKey),
            { IsKeyedService: true, KeyedImplementationInstance: null, KeyedImplementationFactory: null } => ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider,
                serviceDescriptor.KeyedImplementationType!),
            { IsKeyedService: false, ImplementationInstance: not null } => serviceDescriptor.ImplementationInstance,
            { IsKeyedService: false, ImplementationFactory: not null } => serviceDescriptor.ImplementationFactory(serviceProvider),
            _ => ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, serviceDescriptor.ImplementationType!)
        };
}
