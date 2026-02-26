using System.ComponentModel;

namespace Clywell.Core.Cqrs.Dispatching;

/// <summary>
/// Adapts <see cref="IQueryHandler{TQuery,TResult}"/> to <see cref="IHandlerInvoker{TRequest,TResult}"/>
/// so the dispatcher can invoke query handlers without knowing the concrete query type.
/// This type is public to support source-generated DI registration.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class QueryHandlerInvoker<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler) : IHandlerInvoker<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <inheritdoc/>
    public Task<TResult> HandleAsync(TQuery request, CancellationToken ct) =>
        handler.HandleAsync(request, ct);
}
