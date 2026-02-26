using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Clywell.Core.Cqrs.Dispatching;

/// <summary>
/// Default <see cref="IDispatcher"/> implementation. Resolves the correct handler and pipeline
/// behaviors from the DI container, builds the behavior pipeline on first call per request type
/// (MethodInfo cached), then executes it.
/// </summary>
internal sealed class Dispatcher(IServiceProvider sp) : IDispatcher
{

    // MethodInfo for BuildAndExecuteAsync<TRequest, TResult> — cached once, MakeGenericMethod per type pair.
    private static readonly MethodInfo _buildAndExecuteMethod =
        typeof(Dispatcher).GetMethod(
            nameof(BuildAndExecuteAsync),
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    // Per (requestType, resultType) cached closed generic MethodInfo — avoids repeated MakeGenericMethod.
    private static readonly ConcurrentDictionary<(Type, Type), MethodInfo> _methodCache = new();

    /// <inheritdoc/>
    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default) =>
        InvokeAsync<TResult>(command, ct);

    /// <inheritdoc/>
    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
        InvokeAsync<TResult>(query, ct);

    private Task<TResult> InvokeAsync<TResult>(object request, CancellationToken ct)
    {
        var requestType = request.GetType();
        var resultType = typeof(TResult);

        var method = _methodCache.GetOrAdd(
            (requestType, resultType),
            static key => _buildAndExecuteMethod.MakeGenericMethod(key.Item1, key.Item2));

        return (Task<TResult>)method.Invoke(this, [request, ct])!;
    }

    /// <summary>
    /// Fully generic pipeline builder. Called via cached reflection once per unique
    /// (TRequest, TResult) pair; subsequent calls hit the cache and skip MakeGenericMethod.
    /// </summary>
    private async Task<TResult> BuildAndExecuteAsync<TRequest, TResult>(
        TRequest request,
        CancellationToken ct)
        where TRequest : notnull
    {
        var handler = sp.GetRequiredService<IHandlerInvoker<TRequest, TResult>>();
        var behaviors = sp.GetServices<IPipelineBehavior<TRequest, TResult>>().ToList();

        // Build inside-out: iterate behaviors reversed so that the first-registered
        // behavior runs outermost (wraps all subsequent ones).
        RequestHandlerDelegate<TResult> pipeline = c => handler.HandleAsync(request, c);

        foreach (var behavior in Enumerable.Reverse(behaviors))
        {
            var next = pipeline;
            var b = behavior;
            pipeline = c => b.HandleAsync(request, next, c);
        }

        return await pipeline(ct).ConfigureAwait(false);
    }
}
