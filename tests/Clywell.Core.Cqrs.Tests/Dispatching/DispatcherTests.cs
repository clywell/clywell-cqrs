using Clywell.Core.Cqrs.Dispatching;
using Clywell.Core.Cqrs.Extensions;
using Clywell.Core.Cqrs.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clywell.Core.Cqrs.Tests.Dispatching;

public class DispatcherTests
{
    private static IServiceProvider BuildProvider(Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddCqrs();
        services.AddCqrsHandlers(); // source-generated — zero reflection
        extra?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SendAsync_DispatchesCommandToCorrectHandler()
    {
        var sp = BuildProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync(new CreateItemCommand("Test"));

        Assert.NotEqual(Guid.Empty, result);
    }

    [Fact]
    public async Task SendAsync_VoidLikeCommand_ReturnsSuccess()
    {
        var sp = BuildProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync(new DeleteItemCommand(Guid.NewGuid()));

        Assert.True(result);
    }

    [Fact]
    public async Task QueryAsync_DispatchesQueryToCorrectHandler()
    {
        var sp = BuildProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var id = Guid.NewGuid();

        var result = await dispatcher.QueryAsync(new GetItemQuery(id));

        Assert.Equal($"Item-{id}", result);
    }

    [Fact]
    public async Task SendAsync_MultipleCallsSameType_UsesMethodInfoCache()
    {
        var sp = BuildProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // Call twice to exercise the cached MethodInfo path
        var r1 = await dispatcher.SendAsync(new CreateItemCommand("A"));
        var r2 = await dispatcher.SendAsync(new CreateItemCommand("B"));

        Assert.NotEqual(Guid.Empty, r1);
        Assert.NotEqual(Guid.Empty, r2);
    }

    [Fact]
    public async Task SendAsync_WithoutRegisteredHandler_ThrowsInvalidOperationException()
    {
        // Register dispatcher WITHOUT calling AddCqrsHandlers (no handlers registered)
        var services = new ServiceCollection();
        services.AddCqrs();
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var act = async () => await dispatcher.SendAsync(new CreateItemCommand("Test"));

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task SendAsync_PassesCorrectCommandToHandler()
    {
        var tracking = new TrackingCreateItemHandler();
        var services = new ServiceCollection();
        services.AddCqrs();
        // Override with tracking handler and its invoker
        services.AddTransient<ICommandHandler<CreateItemCommand, Guid>>(_ => tracking);
        services.AddTransient<IHandlerInvoker<CreateItemCommand, Guid>>(
            sp => new CommandHandlerInvoker<CreateItemCommand, Guid>(
                sp.GetRequiredService<ICommandHandler<CreateItemCommand, Guid>>()));
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var command = new CreateItemCommand("Widget");

        await dispatcher.SendAsync(command);

        Assert.True(tracking.WasCalled);
        Assert.Equal(command, tracking.ReceivedCommand);
    }
}
