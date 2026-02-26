using System.ComponentModel;

namespace Clywell.Core.Cqrs.Dispatching;

/// <summary>
/// Abstracts the invocation of a typed handler (command or query) so the dispatcher
/// can invoke it without knowing the concrete request type at compile time.
/// This type is public to support source-generated DI registration.
/// </summary>
/// <typeparam name="TRequest">The request type (command or query).</typeparam>
/// <typeparam name="TResult">The result type produced by the handler.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IHandlerInvoker<in TRequest, TResult>
{
    /// <summary>Invokes the underlying handler.</summary>
    public Task<TResult> HandleAsync(TRequest request, CancellationToken ct);
}
