namespace Clywell.Core.Cqrs;

/// <summary>
/// Marker interface for a command that produces a result of type <typeparamref name="TResult"/>.
/// Commands represent write operations with intent to change state.
/// </summary>
/// <typeparam name="TResult">The type returned after the command is handled.</typeparam>
public interface ICommand<TResult>
{
}
