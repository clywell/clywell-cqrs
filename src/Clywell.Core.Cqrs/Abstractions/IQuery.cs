namespace Clywell.Core.Cqrs;

/// <summary>
/// Marker interface for a query that produces a result of type <typeparamref name="TResult"/>.
/// Queries represent read operations and must not change state.
/// </summary>
/// <typeparam name="TResult">The type returned after the query is handled.</typeparam>
public interface IQuery<TResult>
{
}
