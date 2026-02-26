namespace Clywell.Core.Cqrs;

/// <summary>
/// Handles a command of type <typeparamref name="TCommand"/> and returns a result of type
/// <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TCommand">The command type. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result type produced by handling the command.</typeparam>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>Handles the given command.</summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that resolves to the command result.</returns>
    public Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}
