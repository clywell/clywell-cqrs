# Clywell.Core.Cqrs

<!-- Badges -->
[![Build Status](https://github.com/clywell/clywell-cqrs/workflows/CI/badge.svg)](https://github.com/clywell/clywell-cqrs/actions)
[![NuGet Version](https://img.shields.io/nuget/v/Clywell.Core.Cqrs.svg)](https://www.nuget.org/packages/Clywell.Core.Cqrs/)
[![License](https://img.shields.io/github/license/clywell/clywell-cqrs.svg)](LICENSE)

## Description

A custom CQRS (Command Query Responsibility Segregation) framework for the Clywell platform — built without MediatR or any third-party mediator library. Provides command/query interfaces, a lightweight dispatcher, and a composable pipeline behavior system for cross-cutting concerns (logging, validation, transactions).

## Features

- `ICommand<TResult>` / `IQuery<TResult>` — strongly-typed request markers
- `ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>` — handler interfaces
- `IDispatcher` — lightweight dispatcher resolving handlers via DI
- `IPipelineBehavior<TRequest, TResult>` — composable middleware pipeline
- Built-in behaviors: **Logging**, **Validation** (FluentValidation), **Transaction** (via `Clywell.Core.Data`)
- `Result<T>` integration via `Clywell.Primitives` — no exceptions for expected failures
- Microsoft DI registration helpers

## Installation

```bash
dotnet add package Clywell.Core.Cqrs
```

## Quick Start

```csharp
// 1. Register in DI
builder.Services.AddClywellCqrs(typeof(Program).Assembly);

// 2. Define a command
public record CreateOrderCommand(Guid TenantId, string ProductId) : ICommand<Result<Guid>>;

// 3. Implement a handler
public class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(CreateOrderCommand command, CancellationToken ct)
    {
        // business logic here
        return Result.Ok(Guid.NewGuid());
    }
}

// 4. Dispatch
var result = await dispatcher.SendAsync(new CreateOrderCommand(tenantId, productId), ct);
```

## Usage Examples

### Queries

```csharp
public record GetOrderByIdQuery(Guid OrderId) : IQuery<Result<OrderDto>>;

public class GetOrderByIdHandler : IQueryHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> HandleAsync(GetOrderByIdQuery query, CancellationToken ct)
    {
        // query logic
    }
}
```

### Adding FluentValidation

```csharp
public class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
```

Validation errors are returned as `Result.Failure(...)` — no exceptions thrown.

### Custom Pipeline Behavior

```csharp
public class MyBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
{
    public async Task<TResult> HandleAsync(TRequest request, RequestHandlerDelegate<TResult> next, CancellationToken ct)
    {
        // before
        var result = await next();
        // after
        return result;
    }
}
```

## Pipeline Order

```
Request → Logging → Validation → Transaction (commands only) → Handler → Response
```

## API Reference

| Type | Description |
|------|-------------|
| `ICommand<TResult>` | Marker for commands (write operations) |
| `IQuery<TResult>` | Marker for queries (read operations) |
| `ICommandHandler<TCommand, TResult>` | Handler for commands |
| `IQueryHandler<TQuery, TResult>` | Handler for queries |
| `IDispatcher` | Sends commands and queries to their handlers |
| `IPipelineBehavior<TRequest, TResult>` | Middleware in the dispatch pipeline |
| `LoggingBehavior<TRequest, TResult>` | Logs request/response with timing |
| `ValidationBehavior<TRequest, TResult>` | Validates requests via FluentValidation |

## Contributing

See [Backend Development Guide](../docs/BACKEND_DEVELOPMENT_GUIDE.md) for development guidelines.

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history.
