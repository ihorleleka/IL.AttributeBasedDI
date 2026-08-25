using IL.AttributeBasedDI.Attributes;
using IL.AttributeBasedDI.Extensions;
using IL.AttributeBasedDI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace IL.AttributeBasedDI.Tests.DI;

public interface IDescriptorShapeService;

public sealed class DescriptorShapeService : IDescriptorShapeService;

[Decorator(typeof(IDescriptorShapeService))]
public sealed class DescriptorShapeDecorator(IDescriptorShapeService inner) : IDescriptorShapeService
{
    public IDescriptorShapeService Inner { get; } = inner;
}

[Decorator(typeof(IDescriptorShapeService), decorationOrder: 1)]
public sealed class DescriptorShapeDecoratorA(IDescriptorShapeService inner) : IDescriptorShapeService
{
    public IDescriptorShapeService Inner { get; } = inner;
}

[Decorator(typeof(IDescriptorShapeService), decorationOrder: 2)]
public sealed class DescriptorShapeDecoratorB(IDescriptorShapeService inner) : IDescriptorShapeService
{
    public IDescriptorShapeService Inner { get; } = inner;
}

public class DecoratorDescriptorShapeTests
{
    [Fact]
    public void Decorator_Transforms_ImplementationType_Descriptor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDescriptorShapeService, DescriptorShapeService>();

        RegisterDecorator(services, typeof(DescriptorShapeDecorator));

        AssertDecoratorWrapsOriginal(services.BuildServiceProvider());
    }

    [Fact]
    public void Decorator_Transforms_Factory_Descriptor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDescriptorShapeService>(_ => new DescriptorShapeService());

        RegisterDecorator(services, typeof(DescriptorShapeDecorator));

        AssertDecoratorWrapsOriginal(services.BuildServiceProvider());
    }

    [Fact]
    public void Decorator_Transforms_Explicit_ServiceDescriptor()
    {
        var services = new ServiceCollection();
        services.RemoveAll<IDescriptorShapeService>();
        services.Add(ServiceDescriptor.Describe(
            typeof(IDescriptorShapeService),
            typeof(DescriptorShapeService),
            ServiceLifetime.Singleton));

        RegisterDecorator(services, typeof(DescriptorShapeDecorator));

        AssertDecoratorWrapsOriginal(services.BuildServiceProvider());
    }

    [Fact]
    public void Decorators_Respect_DecorationOrder()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDescriptorShapeService, DescriptorShapeService>();

        RegisterDecorator(services, typeof(DescriptorShapeDecoratorA), typeof(DescriptorShapeDecoratorB));

        var service = services.BuildServiceProvider().GetRequiredService<IDescriptorShapeService>();
        var outer = Assert.IsType<DescriptorShapeDecoratorB>(service);
        var middle = Assert.IsType<DescriptorShapeDecoratorA>(outer.Inner);
        Assert.IsType<DescriptorShapeService>(middle.Inner);
    }

    private static void RegisterDecorator(IServiceCollection services, params Type[] decoratorTypes)
    {
        services.RegisterClassesWithDecoratorAttributes(
            new DiRegistrationSummary(services),
            FeaturesNoop.None,
            true,
            decoratorTypes);
    }

    private static void AssertDecoratorWrapsOriginal(ServiceProvider provider)
    {
        var service = provider.GetRequiredService<IDescriptorShapeService>();
        var decorator = Assert.IsType<DescriptorShapeDecorator>(service);
        Assert.IsType<DescriptorShapeService>(decorator.Inner);
    }
}
