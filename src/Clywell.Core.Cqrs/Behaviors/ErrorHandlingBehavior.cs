namespace Clywell.Core.Cqrs.Behaviors;

/// <summary>
/// Abstract base class for error-handling pipeline behaviors. Wraps handler execution in a
/// try/catch and delegates exception processing to <see cref="HandleExceptionAsync"/>, giving
/// subclasses full control over how exceptions are mapped, logged, or re-thrown.
/// </summary>
/// <typeparam name="TRequest">The command or query type.</typeparam>
/// <typeparam name="TResult">The result type produced by the handler.</typeparam>
/// <remarks>
/// <para>
/// This class provides the plumbing (try/catch) without imposing any opinion on what happens
/// when an exception occurs. Subclasses decide the outcome — map to a failure result, write to
/// a logger, publish a metric, rethrow, or any combination.
/// </para>
/// <para>
/// Register your subclass via
/// <see cref="Extensions.ServiceCollectionExtensions.AddCqrsBehavior{TBehavior}"/>.
/// </para>
/// <example>
/// <code>
/// // Map unhandled exceptions to Result&lt;T&gt; failures instead of letting them propagate:
/// public sealed class MyErrorHandlingBehavior&lt;TRequest, TResult&gt;
///     : ErrorHandlingBehavior&lt;TRequest, TResult&gt;
///     where TRequest : notnull
///     where TResult : Result  // your Result type
/// {
///     private readonly ILogger _logger;
///
///     public MyErrorHandlingBehavior(ILogger&lt;MyErrorHandlingBehavior&lt;TRequest, TResult&gt;&gt; logger)
///         => _logger = logger;
///
///     protected override Task&lt;TResult&gt; HandleExceptionAsync(
///         TRequest request,
///         Exception exception,
///         CancellationToken ct)
///     {
///         _logger.LogError(exception, "Unhandled exception in {Request}", typeof(TRequest).Name);
///         return Task.FromResult((TResult)Result.Failure(Error.Unexpected(exception.Message)));
///     }
/// }
///
/// // Registration:
/// services.AddCqrs()
///         .AddCqrsHandlers()
///         .AddCqrsBehavior&lt;MyErrorHandlingBehavior&lt;,&gt;&gt;();
/// </code>
/// </example>
/// </remarks>
public abstract class ErrorHandlingBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken ct = default)
    {
        try
        {
            return await next(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ShouldHandle(ex))
        {
            return await HandleExceptionAsync(request, ex, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Determines whether this behavior should handle the given exception.
    /// Override to narrow exception handling to specific types.
    /// Default implementation handles all non-<see cref="OperationCanceledException"/> exceptions.
    /// </summary>
    /// <param name="exception">The exception thrown by the handler or a downstream behavior.</param>
    /// <returns><see langword="true"/> to intercept; <see langword="false"/> to let it propagate.</returns>
    protected virtual bool ShouldHandle(Exception exception) =>
        exception is not OperationCanceledException;

    /// <summary>
    /// Called when an exception passes the <see cref="ShouldHandle"/> filter.
    /// Implement this method to map, log, or rethrow the exception.
    /// </summary>
    /// <param name="request">The original request that triggered the pipeline.</param>
    /// <param name="exception">The caught exception.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A fallback result to return to the caller, or rethrow the exception (or a new one)
    /// if a fallback result is not appropriate for your use case.
    /// </returns>
    protected abstract Task<TResult> HandleExceptionAsync(
        TRequest request,
        Exception exception,
        CancellationToken ct);
}
