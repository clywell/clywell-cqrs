namespace Clywell.Core.Cqrs.Sample.Features.Items;

public record CreateItemCommand(string Name, string Description) : ICommand<ItemDto>;

public record GetItemQuery(Guid Id) : IQuery<ItemDto>;

public record ItemDto(Guid Id, string Name, string Description);

// In-memory store for demo purposes
internal static class ItemStore
{
    private static readonly Dictionary<Guid, ItemDto> Store = new();

    internal static ItemDto Create(string name, string description)
    {
        var id = Guid.NewGuid();
        var item = new ItemDto(id, name, description);
        Store[id] = item;
        return item;
    }

    internal static ItemDto? Get(Guid id)
        => Store.TryGetValue(id, out var item) ? item : null;
}

public class CreateItemCommandHandler : ICommandHandler<CreateItemCommand, ItemDto>
{
    public Task<ItemDto> HandleAsync(CreateItemCommand command, CancellationToken ct = default)
    {
        var item = ItemStore.Create(command.Name, command.Description);
        return Task.FromResult(item);
    }
}

public class GetItemHandler : IQueryHandler<GetItemQuery, ItemDto>
{
    public Task<ItemDto> HandleAsync(GetItemQuery query, CancellationToken ct = default)
    {
        var item = ItemStore.Get(query.Id) ?? throw new KeyNotFoundException($"Item with ID {query.Id} not found.");
        return Task.FromResult(item);
    }
}
