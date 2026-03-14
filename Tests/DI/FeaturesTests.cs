using System.Text;
using IL.AttributeBasedDI.Attributes;
using IL.AttributeBasedDI.FeatureFlags;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace IL.AttributeBasedDI.Tests.DI;

using Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Flags]
public enum Features
{
    None = 0,
    FeatureA = 1 << 0,
    FeatureB = 1 << 1,
    FeatureC = 1 << 2
}

[Flags]
public enum AnotherOptionsEnum
{
    None = 0,
    FeatureX = 1 << 0,
    FeatureY = 1 << 1
}

[Service<Features>(Feature = Features.FeatureA)]
public class Test1;

[Decorator<Features>(Feature = Features.None)]
public class Test1InactiveDecorator1(Test1 source) : Test1;

[Decorator<Features>(Feature = Features.FeatureB)]
public class Test1InactiveDecorator2(Test1 source) : Test1;

[Service<Features>(Feature = Features.FeatureB)]
public class Test2;

[Service<Features>(Feature = Features.FeatureC)]
public class Test3;

[Decorator<Features>(Feature = Features.FeatureC)]
public class Test3Decorator : Test3;

[Service<Features>(Feature = Features.FeatureA | Features.FeatureC)]
public class TestMultipleFeatures;

[Service<AnotherOptionsEnum>(Feature = AnotherOptionsEnum.FeatureX)]
public class TestFeatureXService;

[Service<AnotherOptionsEnum>(Feature = AnotherOptionsEnum.FeatureY)]
public class TestFeatureYService;

public class FeatureEnabledAttributesTests
{
    [Fact]
    public void OnlyServiceWithActivatedFeaturesSuccessfullyRegistered_AppSettings()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        // Act
        var appSettings = """
                          {
                              "DIFeatureFlags": {
                                "Features": ["FeatureA", "FeatureC"]
                              }
                          }
                          """;

        var builder = new ConfigurationBuilder();
        builder.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appSettings)));
        var configuration = builder.Build();
        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration,
            options =>
            {
                options.SetFeaturesFromConfig(new Dictionary<string, Type>
                {
                    { nameof(Features), typeof(Features) }
                });
            }
        );
        var sp = serviceCollection.BuildServiceProvider();

        // Assert
        var flagSet = sp.GetRequiredService<IOptions<FeatureFlagSet>>();
        Assert.NotNull(flagSet);
        var service1 = sp.GetRequiredService<Test1>();
        Assert.True(service1 is not Test1InactiveDecorator1);
        Assert.True(service1 is not Test1InactiveDecorator2);
        Assert.NotNull(service1);
        var service2 = sp.GetService<Test2>();
        Assert.Null(service2);
        var service3 = sp.GetService<Test3>();
        Assert.NotNull(service3);
        Assert.True(service3 is Test3Decorator);
    }

    [Fact]
    public void OnlyServiceWithActivatedFeaturesSuccessfullyRegistered_AppSettings_CustomKey()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        const string customKey = "FeatureFlags";

        // Act
        var appSettings = $$"""
                            {
                                "{{customKey}}": {
                                  "Features": ["FeatureA", "FeatureC"]
                                }
                            }
                            """;

        var builder = new ConfigurationBuilder();
        builder.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appSettings)));
        var configuration = builder.Build();
        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration,
            options =>
            {
                options.SetFeaturesFromConfig(new Dictionary<string, Type>
                {
                    { nameof(Features), typeof(Features) }
                }, customKey);
            });
        var sp = serviceCollection.BuildServiceProvider();

        // Assert
        var service1 = sp.GetRequiredService<Test1>();
        Assert.NotNull(service1);
        var service2 = sp.GetService<Test2>();
        Assert.Null(service2);
        var service3 = sp.GetService<Test3>();
        Assert.NotNull(service3);
    }

    [Fact]
    public void OnlyServiceWithActivatedFeaturesSuccessfullyRegistered_OptionsAction()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();


        var builder = new ConfigurationBuilder();
        var configuration = builder.Build();
        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration,
            options => options.AddFeature(Features.FeatureA | Features.FeatureC)
        );
        var sp = serviceCollection.BuildServiceProvider();

        // Assert
        var service1 = sp.GetRequiredService<Test1>();
        Assert.NotNull(service1);
        var service2 = sp.GetService<Test2>();
        Assert.Null(service2);
        var service3 = sp.GetService<Test3>();
        Assert.NotNull(service3);
    }

    [Fact]
    public void OnlyServiceWithActivatedFeaturesSuccessfullyRegistered_OptionsAction_Merge()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();


        var builder = new ConfigurationBuilder();
        var configuration = builder.Build();
        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration,
            options =>
            {
                options.AddFeature(Features.FeatureA);
                options.AddFeature(Features.FeatureC);
            }
        );
        var sp = serviceCollection.BuildServiceProvider();

        // Assert
        var service1 = sp.GetRequiredService<Test1>();
        Assert.NotNull(service1);
        var service2 = sp.GetService<Test2>();
        Assert.Null(service2);
        var service3 = sp.GetService<Test3>();
        Assert.NotNull(service3);
    }

    [Fact]
    public void ServiceWithAtLeast1ActivatedFeaturesSuccessfullyRegistered_OptionsAction_Merge()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();


        var builder = new ConfigurationBuilder();
        var configuration = builder.Build();
        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration,
            options => { options.AddFeature(Features.FeatureA); }
        );
        var sp = serviceCollection.BuildServiceProvider();

        // Assert
        var service = sp.GetRequiredService<TestMultipleFeatures>();
        Assert.NotNull(service);
    }

    [Fact]
    public void ServiceWithMultipleActivatedFeaturesSuccessfullyRegistered_OptionsAction_Merge()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        var builder = new ConfigurationBuilder();
        var configuration = builder.Build();
        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration,
            options => { options.AddFeature(Features.FeatureA | Features.FeatureB); }
        );
        var sp = serviceCollection.BuildServiceProvider();

        // Assert
        var service = sp.GetRequiredService<TestMultipleFeatures>();
        Assert.NotNull(service);
    }

    [Fact]
    public void MultipleFeatureEnumTypesSuccessfullyRegistered_OptionsAction()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        var builder = new ConfigurationBuilder();
        var configuration = builder.Build();

        // Act
        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration,
            options =>
            {
                options.AddFeature(Features.FeatureA);
                options.AddFeature(AnotherOptionsEnum.FeatureX);
            }
        );
        var sp = serviceCollection.BuildServiceProvider();

        // Assert
        var serviceA = sp.GetRequiredService<Test1>();
        Assert.NotNull(serviceA);
        var serviceX = sp.GetRequiredService<TestFeatureXService>();
        Assert.NotNull(serviceX);
        var serviceY = sp.GetService<TestFeatureYService>();
        Assert.Null(serviceY);
    }

    [Fact]
    public void MultipleFeatureEnumTypesSuccessfullyRegistered_AppSettingsMapping()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        // Act
        var appSettings = """
                          {
                              "DIFeatureFlags": {
                                "Features": ["FeatureA"],
                                "AnotherOptionsEnum": ["FeatureX"]
                              }
                          }
                          """;

        var builder = new ConfigurationBuilder();
        builder.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appSettings)));
        var configuration = builder.Build();
        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration,
            options =>
            {
                options.SetFeaturesFromConfig(new Dictionary<string, Type>
                {
                    { nameof(Features), typeof(Features) },
                    { nameof(AnotherOptionsEnum), typeof(AnotherOptionsEnum) }
                });
            }
        );
        var sp = serviceCollection.BuildServiceProvider();

        // Assert
        var serviceA = sp.GetRequiredService<Test1>();
        Assert.NotNull(serviceA);
        var serviceX = sp.GetRequiredService<TestFeatureXService>();
        Assert.NotNull(serviceX);
        var serviceY = sp.GetService<TestFeatureYService>();
        Assert.Null(serviceY);
    }

    [Fact]
    public void FeatureFlagSet_IsFeatureActive_ReturnsTrueForIncludedFlag()
    {
        var featureSet = new FeatureFlagSet(Features.FeatureA | Features.FeatureC);

        Assert.True(featureSet.IsFeatureActive(Features.FeatureA));
        Assert.True(featureSet.IsFeatureActive(Features.FeatureC));
        Assert.False(featureSet.IsFeatureActive(Features.FeatureB));
    }
}
