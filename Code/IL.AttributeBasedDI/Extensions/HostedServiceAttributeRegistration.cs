using System.Runtime.InteropServices;
using System.Reflection;
using IL.AttributeBasedDI.Attributes;
using IL.AttributeBasedDI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IL.AttributeBasedDI.Extensions;

internal static class HostedServiceAttributeRegistration
{
    public static void RegisterHostedServices<TFeatureFlag>(this IServiceCollection serviceCollection,
        DiRegistrationSummary diRegistrationSummary,
        TFeatureFlag _,
        params Type[] types)
        where TFeatureFlag : struct, Enum
    {
        var registrations = types
            .Where(type => type.GetCustomAttributes(typeof(HostedServiceAttribute), false).Any())
            .SelectMany(type => type
                .GetCustomAttributes<HostedServiceAttribute>()
                .Select(attribute => (Attribute: attribute, DecoratedType: type)))
            .ToList();

        foreach (var registration in CollectionsMarshal.AsSpan(registrations))
        {
            var attribute = registration.Attribute;
            var implementationType = attribute.ImplementationType ?? registration.DecoratedType;
            if (implementationType is null)
            {
                throw new InvalidOperationException(
                    $"HostedServiceAttribute requires an implementation type when automatic detection is not available.");
            }

            if (!typeof(IHostedService).IsAssignableFrom(implementationType))
            {
                throw new InvalidOperationException(
                    $"Hosted service type '{implementationType.FullName}' must implement {nameof(IHostedService)}.");
            }

            var alreadyRegistered = serviceCollection.Any(descriptor =>
                descriptor.ServiceType == implementationType &&
                descriptor.ImplementationType == implementationType &&
                descriptor.Lifetime == attribute.Lifetime);

            if (!alreadyRegistered)
            {
                var existingSingletonRegistration = serviceCollection.LastOrDefault(descriptor =>
                    descriptor.Lifetime == ServiceLifetime.Singleton &&
                    descriptor.ImplementationType == implementationType &&
                    descriptor.ServiceType != typeof(IHostedService));

                if (existingSingletonRegistration is not null)
                {
                    serviceCollection.AddSingleton(implementationType,
                        serviceProvider => serviceProvider.GetRequiredService(existingSingletonRegistration.ServiceType));
                }
                else
                {
                    serviceCollection.AddServiceWithLifetime(implementationType, null, attribute.Lifetime, null);
                }
            }

            serviceCollection.AddSingleton(typeof(IHostedService),
                serviceProvider => (IHostedService)serviceProvider.GetRequiredService(implementationType));

            diRegistrationSummary.ServiceGraph.AddOrMerge(new RegistrationEntry<TFeatureFlag>
            {
                Lifetime = attribute.Lifetime,
                ServiceType = implementationType,
                ImplementationType = implementationType,
                Key = null,
                Feature = default
            });
        }
    }
}
