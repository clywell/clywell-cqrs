# Clywell.Core.Cqrs

<!-- Badges -->
[![NuGet Version](https://img.shields.io/nuget/v/Clywell.Core.Cqrs.svg)](https://www.nuget.org/packages/Clywell.Core.Cqrs/)
[![License](https://img.shields.io/github/license/clywell/clywell-cqrs.svg)](LICENSE)

## Packages

| Package | Description |
|---------|-------------|
| `Clywell.Core.Cqrs` | Core interfaces, dispatcher, and pipeline infrastructure |
| `Clywell.Core.Cqrs.Generators` | Roslyn source generator — emits compile-time DI handler registration (zero reflection) |
| `Clywell.Core.Cqrs.FluentValidation` | FluentValidation pipeline behavior |

---

## Overview

A lightweight CQRS framework built without MediatR or any third-party mediator dependency. It provides strongly-typed command/query interfaces, a DI-resolved dispatcher, and a composable pipeline behavior system for cross-cutting concerns such as validation, logging, and caching.

The dispatcher resolves handlers and behaviors entirely through the DI container. The companion source generator replaces assembly-scanning reflection with compile-time registration, making the solution trimmer- and NativeAOT-compatible.

---

## Installation

Install the core package plus the source generator (required for handler registration):

```bash
dotnet add package Clywell.Core.Cqrs
dotnet add package Clywell.Core.Cqrs.Generators
```

Optionally add FluentValidation integration:

```bash
dotnet add package Clywell.Core.Cqrs.FluentValidation
```

> **Generator project reference** — the Generators package must be referenced as an analyzer, not a runtime dependency. Add it manually in your `.csproj` if the NuGet tooling does not do this automatically:
>
> ```xml
> <PackageReference Include="Clywell.Core.Cqrs.Generators"
>                   OutputItemType="Analyzer"
>                   ReferenceOutputAssembly="false" />
> ```

---

## Quick Start

### 1. Register services

```csharp
builder.Services
    .AddCqrs()           // registers the IDispatcher
    .AddCqrsHandlers();  // source-generated — registers all handlers, zero reflection
```

### 2. Define a command

```csharp
public record CreateOrderCommand(Guid CustomerId, string ProductId) : ICommand<OrderDto>;
```

### 3. Implement the handler

```csharp
public class CreateOrderHandler : ICommandHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
    {
        // write business logic here
        return new OrderDto(Guid.NewGuid(), command.ProductId);
    }
}
```

### 4. Inject `IDispatcher` and dispatch

```csharp
app.MapPost("/orders", async (IDispatcher dispatcher, CreateOrderRequest request, CancellationToken ct) =>
{
    var command = new CreateOrderCommand(request.CustomerId, request.ProductId);
    var order = await dispatcher.SendAsync(command, ct);
    return Results.Created($"/orders/{order.Id}", order);
});
```

---

## Commands vs Queries

| Concept | Interface | Dispatch method | Intent |
|---------|-----------|-----------------|--------|
| Command | `ICommand<TResult>` | `dispatcher.SendAsync(command, ct)` | Write — changes state |
| Query | `IQuery<TResult>` | `dispatcher.QueryAsync(query, ct)` | Read — must not change state |

### Query example

```csharp
// Define
public record GetOrderByIdQuery(Guid OrderId) : IQuery<OrderDto>;

// Handle
public class GetOrderByIdHandler : IQueryHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<OrderDto> HandleAsync(GetOrderByIdQuery query, CancellationToken ct = default)
    {
        // read-only data access
        return await _repository.GetByIdAsync(query.OrderId, ct)
            ?? throw new KeyNotFoundException($"Order {query.OrderId} not found.");
    }
}

// Dispatch
var order = await dispatcher.QueryAsync(new GetOrderByIdQuery(orderId), ct);
```

---

## Pipeline Behaviors

`IPipelineBehavior<TRequest, TResult>` is the middleware contract. Behaviors wrap handler execution and run in the order they are registered in DI — the first registered behavior is the outermost wrapper.

### How the pipeline works

```
Dispatcher
  └── Behavior 1 (outermost — registered first)
        └── Behavior 2
              └── Behavior N (innermost — registered last)
                    └── Handler
```

When `SendAsync` or `QueryAsync` is called the dispatcher:
1. Resolves the handler via the DI container.
2. Resolves all registered `IPipelineBehavior<TRequest, TResult>` instances for that request type.
3. Builds a delegate chain from innermost to outermost.
4. Invokes the outermost delegate — execution flows through every behavior before reaching the handler and unwinds the same way on the return path.

### Implementing a custom behavior

```csharp
public class LoggingBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResult>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResult>> logger)
        => _logger = logger;

    public async Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken ct = default)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {Request}", requestName);

        var result = await next(ct);  // call the next behavior or handler

        _logger.LogInformation("Handled {Request}", requestName);
        return result;
    }
}
```

### Registering a custom behavior

Register open-generic behaviors so they apply to every request type, or use a closed-generic registration to target a specific request:

```csharp
// applies to every command and query
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// applies only to CreateOrderCommand
services.AddTransient<IPipelineBehavior<CreateOrderCommand, OrderDto>, AuditBehavior>();
```

---

## FluentValidation Integration

`Clywell.Core.Cqrs.FluentValidation` provides `ValidationBehavior<TRequest, TResult>` — a pipeline behavior that runs all registered `IValidator<TRequest>` instances before the handler is invoked.

### Registration

```csharp
builder.Services
    .AddCqrs()
    .AddCqrsHandlers()
    .AddCqrsFluentValidation(typeof(CreateOrderValidator).Assembly);
```

`AddCqrsFluentValidation` registers `ValidationBehavior<,>` as an open-generic transient pipeline behavior and scans the provided assemblies for all `IValidator<T>` implementations.

### Define validators

```csharp
public class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required.");
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("Customer ID is required.");
    }
}
```

### Validation failures

When validation fails, `ValidationBehavior` throws `FluentValidation.ValidationException` containing all aggregated failures. Handle this at the application boundary — for example with a global exception handler middleware:

```csharp
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is ValidationException validationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Validation failed",
                errors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage))
            });
        }
    });
});
```

> The behavior is a **no-op** when no `IValidator<TRequest>` is registered for the incoming request type — it simply calls `next` and continues.

---

## Source Generator (`Clywell.Core.Cqrs.Generators`)

The generator scans your compilation for concrete `ICommandHandler<,>` and `IQueryHandler<,>` implementations at **build time** and emits an `AddCqrsHandlers()` extension method. This replaces reflection-based assembly scanning and is compatible with NativeAOT and .NET trimming.

Generated output example (abbreviated):

```csharp
// <auto-generated/>
public static partial class CqrsHandlerRegistrationExtensions
{
    public static IServiceCollection AddCqrsHandlers(this IServiceCollection services)
    {
        services.AddTransient<IHandlerInvoker<CreateOrderCommand, OrderDto>,
                              CommandHandlerInvoker<CreateOrderCommand, OrderDto>>();
        services.AddTransient<ICommandHandler<CreateOrderCommand, OrderDto>,
                              CreateOrderHandler>();
        // ... one entry per discovered handler
        return services;
    }
}
```

No assembly scanning. No reflection at startup.

---

## Full Registration Example

```csharp
builder.Services
    .AddCqrs()                                                         // IDispatcher
    .AddCqrsHandlers()                                                 // source-generated handlers
    .AddCqrsFluentValidation(typeof(CreateOrderValidator).Assembly)    // validation behavior + validators
    .AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>)); // custom behavior
```

Because behaviors run in registration order, the setup above produces:

```
Request → LoggingBehavior → ValidationBehavior → Handler → Response
```

---

## API Reference

### `Clywell.Core.Cqrs`

| Type | Kind | Description |
|------|------|-------------|
| `ICommand<TResult>` | Interface | Marker for commands (write operations) |
| `IQuery<TResult>` | Interface | Marker for queries (read-only operations) |
| `ICommandHandler<TCommand, TResult>` | Interface | Implement to handle a specific command |
| `IQueryHandler<TQuery, TResult>` | Interface | Implement to handle a specific query |
| `IDispatcher` | Interface | Dispatches commands (`SendAsync`) and queries (`QueryAsync`) through the pipeline |
| `IPipelineBehavior<TRequest, TResult>` | Interface | Middleware that wraps handler execution |
| `RequestHandlerDelegate<TResult>` | Delegate | Represents the next step in the pipeline; invoke inside a behavior to continue |
| `AddCqrs()` | Extension | Registers `IDispatcher` in the DI container |

### `Clywell.Core.Cqrs.Generators`

| Type | Kind | Description |
|------|------|-------------|
| `CqrsHandlerRegistrationGenerator` | Source generator | Emits `AddCqrsHandlers()` at compile time; registers all handlers — zero runtime reflection |

### `Clywell.Core.Cqrs.FluentValidation`

| Type | Kind | Description |
|------|------|-------------|
| `ValidationBehavior<TRequest, TResult>` | Pipeline behavior | Runs all `IValidator<TRequest>` instances; throws `ValidationException` on failure |
| `AddCqrsFluentValidation(assemblies)` | Extension | Registers `ValidationBehavior<,>` and scans assemblies for validators |

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit changes: `git commit -m 'feat: add my feature'`
4. Push to branch: `git push origin feature/my-feature`
5. Create a Pull Request

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history.
