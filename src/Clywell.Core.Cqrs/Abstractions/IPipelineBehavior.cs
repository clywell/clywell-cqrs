namespace Clywell.Core.Cqrs;

/// <summary>
/// Delegate representing the next step in a request pipeline. Invoked by each
/// <see cref="IPipelineBehavior{TRequest,TResult}"/> to continue execution toward the handler.
/// </summary>
/// <typeparam name="TResult">The result type produced by the pipeline.</typeparam>
/// <param name="ct">Cancellation token forwarded through the pipeline.</param>
/// <returns>A task that resolves to the pipeline result.</returns>
public delegate Task<TResult> RequestHandlerDelegate<TResult>(CancellationToken ct = default);

/// <summary>
/// Middleware that wraps handler execution in the dispatch pipeline.
/// Implementations are executed in registration order (outermost first).
/// </summary>
/// <typeparam name="TRequest">The request type (command or query).</typeparam>
/// <typeparam name="TResult">The result type produced by the handler.</typeparam>
/// <example>
/// <code>
/// public class MyBehavior&lt;TRequest, TResult&gt; : IPipelineBehavior&lt;TRequest, TResult&gt;
///     where TRequest : notnull
/// {
///     public async Task&lt;TResult&gt; HandleAsync(
///         TRequest request,
///         RequestHandlerDelegate&lt;TResult&gt; next,
///         CancellationToken ct = default)
///     {
///         // before handler
///         var result = await next(ct);
///         // after handler
///         return result;
///     }
/// }
/// </code>
/// </example>
public interface IPipelineBehavior<in TRequest, TResult>
    where TRequest : notnull
{
    /// <summary>
    /// Processes the request, optionally calling <paramref name="next"/> to continue down the pipeline.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="next">Delegate to invoke the next behavior or handler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that resolves to the result.</returns>
    public Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken ct = default);
}
