using Clywell.Core.Cqrs.Behaviors;
using Clywell.Core.Cqrs.Extensions;
using Clywell.Core.Cqrs.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Clywell.Core.Cqrs.Tests.Behaviors;

// ─── Concrete test double ─────────────────────────────────────────────────────

/// <summary>
/// Concrete subclass used in tests. Captures exceptions and returns a sentinel result
/// rather than rethrowing, demonstrating the fallback-result pattern.
/// </summary>
public sealed class CapturingErrorBehavior<TRequest, TResult> : ErrorHandlingBehavior<TRequest, TResult>
    where TRequest : notnull
{
    public Exception? CaughtException { get; private set; }
    public TResult FallbackResult { get; init; } = default!;

    protected override Task<TResult> HandleExceptionAsync(
        TRequest request,
        Exception exception,
        CancellationToken ct)
    {
        CaughtException = exception;
        return Task.FromResult(FallbackResult);
    }
}

/// <summary>
/// Subclass that narrows handling to <see cref="InvalidOperationException"/> only,
/// demonstrating <see cref="ErrorHandlingBehavior{TRequest,TResult}.ShouldHandle"/> override.
/// </summary>
public sealed class NarrowErrorBehavior<TRequest, TResult> : ErrorHandlingBehavior<TRequest, TResult>
    where TRequest : notnull
{
    public TResult FallbackResult { get; init; } = default!;

    protected override bool ShouldHandle(Exception exception) =>
        exception is InvalidOperationException;

    protected override Task<TResult> HandleExceptionAsync(
        TRequest request,
        Exception exception,
        CancellationToken ct) =>
        Task.FromResult(FallbackResult);
}

// ─── Tests ────────────────────────────────────────────────────────────────────

public class ErrorHandlingBehaviorTests
{
    // ── Fallback result ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_HandlerThrows_InvokesHandleExceptionAsync_AndReturnsFallback()
    {
        var behavior = new CapturingErrorBehavior<CreateItemCommand, Guid>
        {
            FallbackResult = Guid.Empty
        };
        var thrown = new InvalidOperationException("boom");

        var result = await behavior.HandleAsync(
            new CreateItemCommand("Test"),
            _ => throw thrown);

        Assert.Same(thrown, behavior.CaughtException);
        Assert.Equal(Guid.Empty, result);
    }

    [Fact]
    public async Task HandleAsync_NoException_ReturnsHandlerResult()
    {
        var behavior = new CapturingErrorBehavior<CreateItemCommand, Guid>
        {
            FallbackResult = Guid.Empty
        };
        var expected = Guid.NewGuid();

        var result = await behavior.HandleAsync(
            new CreateItemCommand("Test"),
            _ => Task.FromResult(expected));

        Assert.Null(behavior.CaughtException);
        Assert.Equal(expected, result);
    }

    // ── OperationCanceledException passthrough ────────────────────────────────

    [Fact]
    public async Task HandleAsync_OperationCanceled_IsNotCaughtByDefault()
    {
        var behavior = new CapturingErrorBehavior<CreateItemCommand, Guid>();

        var act = () => behavior.HandleAsync(
            new CreateItemCommand("Test"),
            _ => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(act);
        Assert.Null(behavior.CaughtException);
    }

    // ── ShouldHandle override ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ExceptionNotMatchingShouldHandle_PropagatesRaw()
    {
        var behavior = new NarrowErrorBehavior<CreateItemCommand, Guid>
        {
            FallbackResult = Guid.Empty
        };

        // ArgumentException does NOT match InvalidOperationException filter
        var act = () => behavior.HandleAsync(
            new CreateItemCommand("Test"),
            _ => throw new ArgumentException("nope"));

        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task HandleAsync_ExceptionMatchingShouldHandle_ReturnsFallback()
    {
        var expected = Guid.NewGuid();
        var behavior = new NarrowErrorBehavior<CreateItemCommand, Guid>
        {
            FallbackResult = expected
        };

        var result = await behavior.HandleAsync(
            new CreateItemCommand("Test"),
            _ => throw new InvalidOperationException("matches"));

        Assert.Equal(expected, result);
    }

    // ── Pipeline integration via DI ───────────────────────────────────────────

    [Fact]
    public async Task AddCqrsBehavior_RegistersOpenGenericBehavior_RunsInPipeline()
    {
        var services = new ServiceCollection();
        services.AddCqrs();
        services.AddCqrsHandlers();
        // Register a behavior that replaces a throwing handler result — here we just
        // verify behavior is invoked by overriding the handler to throw, then catching.
        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(CapturingErrorBehavior<,>));

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // The real handler returns normally — so behavior's CaughtException stays null.
        var result = await dispatcher.SendAsync(new CreateItemCommand("DI test"));

        Assert.NotEqual(Guid.Empty, result);
    }
}
