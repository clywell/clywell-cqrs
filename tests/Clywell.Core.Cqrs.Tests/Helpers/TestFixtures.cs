namespace Clywell.Core.Cqrs.Tests.Helpers;

// ─── Commands ────────────────────────────────────────────────────────────────

public record CreateItemCommand(string Name) : ICommand<Guid>;

public record DeleteItemCommand(Guid Id) : ICommand<bool>;

// ─── Queries ─────────────────────────────────────────────────────────────────

public record GetItemQuery(Guid Id) : IQuery<string>;

// ─── Handlers ────────────────────────────────────────────────────────────────

public class CreateItemHandler : ICommandHandler<CreateItemCommand, Guid>
{
    public Task<Guid> HandleAsync(CreateItemCommand command, CancellationToken ct = default) =>
        Task.FromResult(Guid.NewGuid());
}

public class DeleteItemHandler : ICommandHandler<DeleteItemCommand, bool>
{
    public Task<bool> HandleAsync(DeleteItemCommand command, CancellationToken ct = default) =>
        Task.FromResult(true);
}

public class GetItemHandler : IQueryHandler<GetItemQuery, string>
{
    public Task<string> HandleAsync(GetItemQuery query, CancellationToken ct = default) =>
        Task.FromResult($"Item-{query.Id}");
}

// ─── Tracking handler (records whether it was called) ───────────────────────

public class TrackingCreateItemHandler : ICommandHandler<CreateItemCommand, Guid>
{
    public bool WasCalled { get; private set; }
    public CreateItemCommand? ReceivedCommand { get; private set; }

    public Task<Guid> HandleAsync(CreateItemCommand command, CancellationToken ct = default)
    {
        WasCalled = true;
        ReceivedCommand = command;
        return Task.FromResult(Guid.NewGuid());
    }
}
