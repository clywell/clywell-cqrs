using System.ComponentModel;

namespace Clywell.Core.Cqrs.Dispatching;

/// <summary>
/// Adapts <see cref="ICommandHandler{TCommand,TResult}"/> to <see cref="IHandlerInvoker{TRequest,TResult}"/>
/// so the dispatcher can invoke command handlers without knowing the concrete command type.
/// This type is public to support source-generated DI registration.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CommandHandlerInvoker<TCommand, TResult>(ICommandHandler<TCommand, TResult> handler) : IHandlerInvoker<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <inheritdoc/>
    public Task<TResult> HandleAsync(TCommand request, CancellationToken ct) =>
        handler.HandleAsync(request, ct);
}
