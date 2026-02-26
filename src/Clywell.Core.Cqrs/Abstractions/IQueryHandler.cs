namespace Clywell.Core.Cqrs;

/// <summary>
/// Handles a query of type <typeparamref name="TQuery"/> and returns a result of type
/// <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TQuery">The query type. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result type produced by handling the query.</typeparam>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>Handles the given query.</summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that resolves to the query result.</returns>
    public Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
