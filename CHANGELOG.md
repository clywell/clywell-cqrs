# Changelog

All notable changes to Clywell.Core.Cqrs will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.1] - 2026-02-27

### Changed

#### `Clywell.Core.Cqrs`
- Source generator (`CqrsHandlerRegistrationGenerator`) is no longer published as a separate `Clywell.Core.Cqrs.Generators` NuGet package — it is now bundled directly inside `Clywell.Core.Cqrs` and activated automatically; no separate package install or project reference required

## [1.0.0] - 2026-02-27

### Added

#### `Clywell.Core.Cqrs` — Core package

- `ICommand<TResult>` — Marker interface for commands (write operations that change state and return a result)
- `ICommandHandler<TCommand, TResult>` — Interface for command handlers; implement to handle a specific command type
- `IQuery<TResult>` — Marker interface for queries (read-only operations that must not change state)
- `IQueryHandler<TQuery, TResult>` — Interface for query handlers; implement to handle a specific query type
- `IDispatcher` — Dispatches commands and queries through the pipeline to their registered handlers
  - `SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct)` — dispatches a command
  - `QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct)` — dispatches a query
- `IPipelineBehavior<TRequest, TResult>` — Middleware interface for wrapping handler execution; implement to create cross-cutting behaviors (logging, validation, caching, etc.)
- `RequestHandlerDelegate<TResult>` — Delegate representing the next step in the pipeline; invoke it inside a behavior to continue execution
- `Dispatcher` — Default `IDispatcher` implementation; resolves handlers and executes behaviors in registration order
- `AddCqrs()` — `IServiceCollection` extension method that registers the dispatcher; call `AddCqrsHandlers()` afterwards to register handlers
- `Clywell.Core.Cqrs.Generators` — Roslyn incremental source generator published as a separate package; emits a compile-time `AddCqrsHandlers()` extension method; scans for all `ICommandHandler<,>` and `IQueryHandler<,>` implementations and registers them with the DI container — zero reflection at runtime

#### `Clywell.Core.Cqrs.FluentValidation` — FluentValidation integration package

- `ValidationBehavior<TRequest, TResult>` — Pipeline behavior that resolves all `IValidator<TRequest>` instances registered in the container and runs them sequentially against the incoming request before dispatching to the handler
  - Validators are executed in sequence (not in parallel) to ensure consistent, deterministic validation order and correct cancellation propagation
  - Aggregates all validation failures across all validators before throwing
  - Throws `FluentValidation.ValidationException` when one or more validation failures are found; handle this at the application boundary (middleware, exception filter, minimal API `Results.Problem`) to map to the desired response format
- `AddCqrsFluentValidation(params Assembly[] assemblies)` — `IServiceCollection` extension method that registers `ValidationBehavior<,>` as an open-generic transient pipeline behavior and scans the provided assemblies for `IValidator<T>` implementations using `AddValidatorsFromAssemblies`

#### `Clywell.Core.Cqrs.Sample` — Sample minimal API project

- Demonstrates end-to-end usage of the core and FluentValidation packages together
- `CreateItemCommand` / `CreateItemCommandHandler` — creates a new in-memory item and returns the created `ItemDto`
- `GetItemQuery` / `GetItemQueryHandler` — retrieves an item by ID or throws `KeyNotFoundException` if not found
- `CreateItemCommandValidator` — FluentValidation rules: name required (max 100 chars), description required (max 500 chars)
- Global exception handler middleware mapping `FluentValidation.ValidationException` to Problem Details (RFC 7807) `400 Bad Request` responses
- OpenAPI documentation with Scalar interactive UI at `/scalar/v1`
- `sample.http` — REST client test file covering both valid requests and validation-error scenarios

[Unreleased]: https://github.com/clywell/clywell-cqrs/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/clywell/clywell-cqrs/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/clywell/clywell-cqrs/releases/tag/v1.0.0
