using IL.AttributeBasedDI.Attributes;
using IL.AttributeBasedDI.Extensions;
using IL.AttributeBasedDI.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Channels;
using Xunit;

namespace IL.AttributeBasedDI.Tests.DI;

public interface ICommonInterface;

[Service(Key = "test-original1")]
public class OriginalService1 : ICommonInterface;

[Service(Key = "test-original2")]
public class OriginalService2 : ICommonInterface;

public class IntermediateService;

[Decorator(Key = "test-*", ServiceType = typeof(ICommonInterface))]
public class DecoratorOfTestServices(ICommonInterface originalService1Or2) : IntermediateService, ICommonInterface
{
    public Type DecoratedService() => originalService1Or2.GetType();
}

[Service]
public class OriginalNonKeyedService : ICommonInterface;

[Decorator(typeof(ICommonInterface))]
public class OriginalNonKeyedServiceDecorator(ICommonInterface originalService) : IntermediateService, ICommonInterface
{
    public Type DecoratedService() => originalService.GetType();
}

[Service(typeof(Test123))]
[ServiceWithOptions<OptionsProvier>(typeof(Test123))]
public class Test123
{
}

public class OptionsProvier : IServiceConfiguration
{
    public static string? ConfigurationPath { get; } = string.Empty;
}

public static class SpecializedInstanceFactory
{
    public static Channel<Func<IServiceProvider, CancellationToken, Task>> CreateBackgroundWorkDelegateChannel()
        => Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>();

    public static Channel<ContentRefreshBatchHostedService.WorkItem> CreateWorkItemChannel()
        => Channel.CreateUnbounded<ContentRefreshBatchHostedService.WorkItem>();
}

public sealed class WorkItemChannelProvider : IImplementationInstanceProvider<Channel<ContentRefreshBatchHostedService.WorkItem>>
{
    public static Channel<ContentRefreshBatchHostedService.WorkItem> GetImplementationInstance()
        => Channel.CreateUnbounded<ContentRefreshBatchHostedService.WorkItem>();
}

public sealed class BackgroundWorkDelegateChannelProvider : IImplementationInstanceProvider<Channel<Func<IServiceProvider, CancellationToken, Task>>>
{
    public static Channel<Func<IServiceProvider, CancellationToken, Task>> GetImplementationInstance()
        => SpecializedInstanceFactory.CreateBackgroundWorkDelegateChannel();
}

[ImplementationInstanceFor<Channel<Func<IServiceProvider, CancellationToken, Task>>, BackgroundWorkDelegateChannelProvider>]
public class BackgroundWorkDelegateChannelRegistrationMarker;

[ImplementationInstanceFor<Channel<ContentRefreshBatchHostedService.WorkItem>, WorkItemChannelProvider>]
public class WorkItemChannelRegistrationMarker;

[ImplementationInstanceFor<Channel<ContentRefreshBatchHostedService.WorkItem>, WorkItemChannelProvider>]
public class WorkItemChannelRegistrationMarkerWithGenericShorthand;

public class BackgroundWorkSchedulerHostedService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public class ContentRefreshBatchHostedService : BackgroundService
{
    public sealed class WorkItem;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

[Service(Lifetime = ServiceLifetime.Singleton, ServiceType = typeof(IContentRefreshWorkScheduler))]
[ImplementationInstanceFor<
    Channel<ContentRefreshBatchHostedServiceWithInlineAttributes.WorkItem>,
    ContentRefreshBatchHostedServiceWithInlineAttributes>]
[HostedService]
internal sealed class ContentRefreshBatchHostedServiceWithInlineAttributes(
    Channel<ContentRefreshBatchHostedServiceWithInlineAttributes.WorkItem> channel)
    : BackgroundService, IContentRefreshWorkScheduler, IImplementationInstanceProvider<Channel<ContentRefreshBatchHostedServiceWithInlineAttributes.WorkItem>>
{
    public sealed class WorkItem;

    public static Channel<WorkItem> GetImplementationInstance() => System.Threading.Channels.Channel.CreateUnbounded<WorkItem>();

    public Channel<WorkItem> Channel { get; } = channel;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public interface IContentRefreshWorkScheduler;

[HostedService(typeof(BackgroundWorkSchedulerHostedService))]
public class BackgroundWorkSchedulerHostedServiceRegistrationMarker;

[HostedService<ContentRefreshBatchHostedService>]
public class ContentRefreshBatchHostedServiceRegistrationMarker;

public class ServiceRegistration
{
    [Fact]
    public void DefaultServiceRegistration()
    {
        var serviceCollection = new ServiceCollection();

        var builder = new ConfigurationBuilder();
        var configuration = builder.Build();
        var res = serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration);
        var sp = serviceCollection.BuildServiceProvider();

        Assert.NotNull(sp.GetService<Test123>());
    }

    [Fact]
    public void DecoratorOfTestServices_ShouldDecorate_OriginalService()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        var builder = new ConfigurationBuilder();
        var configuration = builder.Build();
        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration);
        var sp = serviceCollection.BuildServiceProvider();

        // Act
        var decoratedService1 = sp.GetKeyedService<ICommonInterface>("test-original1");
        var decoratedService2 = sp.GetKeyedService<ICommonInterface>("test-original2");

        var decoratedNonKeyedService = sp.GetService<ICommonInterface>();

        // Assert
        Assert.NotNull(decoratedService1);
        Assert.IsType<DecoratorOfTestServices>(decoratedService1);
        Assert.NotNull(decoratedService2);
        Assert.IsType<DecoratorOfTestServices>(decoratedService2);
        Assert.NotNull(decoratedNonKeyedService);
        Assert.IsType<OriginalNonKeyedServiceDecorator>(decoratedNonKeyedService);

        var decorator1 = (DecoratorOfTestServices)decoratedService1;
        Assert.Equal(typeof(OriginalService1), decorator1.DecoratedService());

        var decorator2 = (DecoratorOfTestServices)decoratedService2;
        Assert.Equal(typeof(OriginalService2), decorator2.DecoratedService());

        var decorator3 = (OriginalNonKeyedServiceDecorator)decoratedNonKeyedService;
        Assert.Equal(typeof(OriginalNonKeyedService), decorator3.DecoratedService());
    }

    [Fact]
    public void ImplementationInstanceAttributes_ShouldRegisterSingletonInstances()
    {
        var serviceCollection = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration);
        var sp = serviceCollection.BuildServiceProvider();

        var workDelegateChannel = sp.GetRequiredService<Channel<Func<IServiceProvider, CancellationToken, Task>>>();
        var workItemChannel = sp.GetRequiredService<Channel<ContentRefreshBatchHostedService.WorkItem>>();

        Assert.NotNull(workDelegateChannel);
        Assert.NotNull(workItemChannel);
        Assert.Same(workDelegateChannel, sp.GetRequiredService<Channel<Func<IServiceProvider, CancellationToken, Task>>>());
        Assert.Same(workItemChannel, sp.GetRequiredService<Channel<ContentRefreshBatchHostedService.WorkItem>>());
    }

    [Fact]
    public void GenericImplementationInstanceShorthand_ShouldRegisterRequestedServiceType()
    {
        var serviceCollection = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration);
        var sp = serviceCollection.BuildServiceProvider();

        var channel = sp.GetRequiredService<Channel<ContentRefreshBatchHostedService.WorkItem>>();
        Assert.NotNull(channel);
    }

    [Fact]
    public void HostedServiceAttributes_ShouldRegisterConcreteHostedServicesAndIHostedServiceEntries()
    {
        var serviceCollection = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration);
        var sp = serviceCollection.BuildServiceProvider();

        var scheduler = sp.GetRequiredService<BackgroundWorkSchedulerHostedService>();
        var batch = sp.GetRequiredService<ContentRefreshBatchHostedService>();
        var hostedServices = sp.GetServices<IHostedService>().ToList();

        Assert.NotNull(scheduler);
        Assert.NotNull(batch);
        Assert.Contains(scheduler, hostedServices);
        Assert.Contains(batch, hostedServices);
    }

    [Fact]
    public void SingleClass_CanHave_Service_ImplementationInstance_And_HostedService_Attributes()
    {
        var serviceCollection = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        serviceCollection.AddServiceAttributeBasedDependencyInjection(configuration);
        var sp = serviceCollection.BuildServiceProvider();

        var scheduler = sp.GetRequiredService<IContentRefreshWorkScheduler>();
        var channel = sp.GetRequiredService<Channel<ContentRefreshBatchHostedServiceWithInlineAttributes.WorkItem>>();
        var hostedServices = sp.GetServices<IHostedService>().ToList();

        Assert.NotNull(scheduler);
        Assert.NotNull(channel);
        Assert.Contains(hostedServices, service => service is ContentRefreshBatchHostedServiceWithInlineAttributes);
    }
}
