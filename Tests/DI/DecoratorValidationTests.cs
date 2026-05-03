using IL.AttributeBasedDI.Attributes;
using IL.AttributeBasedDI.Exceptions;
using IL.AttributeBasedDI.Extensions;
using IL.AttributeBasedDI.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IL.AttributeBasedDI.Tests.DI;

[Flags]
public enum DecoratorValidationFeature
{
    None = 0,
    Enabled = 1
}

[Service<DecoratorValidationFeature>(Feature = DecoratorValidationFeature.Enabled)]
[Decorator<DecoratorValidationFeature>(typeof(SelfDecoratingService), Feature = DecoratorValidationFeature.Enabled)]
public class SelfDecoratingService;

[Service<DecoratorValidationFeature>(Feature = DecoratorValidationFeature.Enabled)]
public class ChainRootService;

[Decorator<DecoratorValidationFeature>(typeof(ChainRootService), DecorationOrder = 1, Feature = DecoratorValidationFeature.Enabled)]
public class ChainDecoratorA1(ChainRootService source) : ChainRootService;

[Decorator<DecoratorValidationFeature>(typeof(ChainRootService), DecorationOrder = 2, Feature = DecoratorValidationFeature.Enabled)]
public class ChainDecoratorB(ChainRootService source) : ChainRootService;

public class DecoratorValidationTests
{
    [Fact]
    public void RegisterClassesWithDecoratorAttributes_WhenTypeDecoratesItself_ThrowsServiceDecorationException()
    {
        var serviceCollection = new ServiceCollection();
        var diSummary = new DiRegistrationSummary(serviceCollection);
        var types = new[] { typeof(SelfDecoratingService) };

        serviceCollection.RegisterClassesWithServiceAttributes(diSummary, DecoratorValidationFeature.Enabled, types);

        var ex = Assert.Throws<ServiceDecorationException>(() =>
            serviceCollection.RegisterClassesWithDecoratorAttributes(diSummary, DecoratorValidationFeature.Enabled, true, types));

        Assert.Contains("cannot decorate itself", ex.Message);
    }

    [Fact]
    public void RegisterClassesWithDecoratorAttributes_WhenDecoratorAppearsTwiceInChain_ThrowsServiceDecorationException()
    {
        var serviceCollection = new ServiceCollection();
        var diSummary = new DiRegistrationSummary(serviceCollection);
        var types = new[] { typeof(ChainRootService), typeof(ChainDecoratorA1), typeof(ChainDecoratorB), typeof(ChainDecoratorA1) };

        serviceCollection.RegisterClassesWithServiceAttributes(diSummary, DecoratorValidationFeature.Enabled, types);

        var ex = Assert.Throws<ServiceDecorationException>(() =>
            serviceCollection.RegisterClassesWithDecoratorAttributes(diSummary, DecoratorValidationFeature.Enabled, true, types));

        Assert.Contains("appears more than once in the same decoration chain", ex.Message);
    }
}
