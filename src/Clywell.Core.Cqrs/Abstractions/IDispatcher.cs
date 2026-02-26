namespace Clywell.Core.Cqrs;

/// <summary>
/// Dispatches commands and queries to their registered handlers, running them through
/// the configured pipeline behaviors.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Dispatches a command through the pipeline and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The command result type.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that resolves to the command result.</returns>
    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);

    /// <summary>
    /// Dispatches a query through the pipeline and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The query result type.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that resolves to the query result.</returns>
    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
}
