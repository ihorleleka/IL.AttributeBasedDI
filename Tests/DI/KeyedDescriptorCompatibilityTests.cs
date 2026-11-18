using IL.AttributeBasedDI.Extensions;
using IL.AttributeBasedDI.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IL.AttributeBasedDI.Tests.DI;

public class KeyedDescriptorCompatibilityTests
{
    [Theory]
    [InlineData("type")]
    [InlineData("factory")]
    [InlineData("instance")]
    public void KeyedDecorator_PreservesSingletonAndOriginalDescriptorShape(string descriptorShape)
    {
        const string key = "test-descriptor";
        var services = new ServiceCollection();
        object? receivedFactoryKey = null;

        switch (descriptorShape)
        {
            case "type":
                services.AddKeyedSingleton<ICommonInterface, OriginalService1>(key);
                break;
            case "factory":
                services.AddKeyedSingleton<ICommonInterface>(key, (_, serviceKey) =>
                {
                    receivedFactoryKey = serviceKey;
                    return new OriginalService1();
                });
                break;
            case "instance":
                services.AddKeyedSingleton<ICommonInterface>(key, new OriginalService1());
                break;
        }

        services.RegisterClassesWithDecoratorAttributes(
            new DiRegistrationSummary(services),
            FeaturesNoop.None,
            true,
            typeof(DecoratorOfTestServices));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var decorated = Assert.IsType<DecoratorOfTestServices>(provider.GetRequiredKeyedService<ICommonInterface>(key));

        Assert.Equal(typeof(OriginalService1), decorated.DecoratedService());
        Assert.Same(decorated, provider.GetRequiredKeyedService<ICommonInterface>(key));
        Assert.Null(provider.GetService<ICommonInterface>());
        if (descriptorShape == "factory")
        {
            Assert.Equal(key, receivedFactoryKey);
        }
    }
}
