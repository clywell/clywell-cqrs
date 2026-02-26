# Clywell.Core.Cqrs Sample — Minimal API Project

This sample demonstrates a complete minimal API project using the Clywell CQRS library with FluentValidation pipeline behavior.

## Features

- **Pure CQRS Pipeline**: Commands and queries dispatched through a configurable pipeline
- **FluentValidation Integration**: Automatic request validation with declarative rules
- **Source-Generated Handler Registration**: Zero-reflection handler discovery via Roslyn
- **Global Exception Handler**: Maps `ValidationException` to standard Problem Details responses
- **OpenAPI / Scalar Docs**: Built-in API documentation UI

## Project Structure

```
Features/
  Items/
    ItemHandlers.cs          — Commands, queries, handlers, validators
    CreateItemCommandValidator.cs — FluentValidation rules

Program.cs                   — DI setup, endpoints, global error handler
ErrorHandlingExtensions.cs   — Exception mapping middleware
```

## Running the Sample

```bash
# Start the server
dotnet run --project samples/Clywell.Core.Cqrs.Sample

# Navigate to the interactive API docs
# http://localhost:5000/scalar/v1
```

## DI Registration

```csharp
builder.Services
    .AddCqrs()                                    // Register dispatcher
    .AddCqrsHandlers()                           // Source-generated handler registration
    .AddCqrsFluentValidation(
        typeof(CreateItemCommandValidator).Assembly  // Scan and register validators
    );
```

## Endpoints

### Create Item (Command)
```http
POST /api/items
Content-Type: application/json

{
  "name": "Widget",
  "description": "A useful widget"
}
```

**Response** (201):
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Widget",
  "description": "A useful widget"
}
```

**Validation Error** (400):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "name": ["Name is required."]
  }
}
```

### Get Item (Query)
```http
GET /api/items/{id:guid}
```

**Response** (200):
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Widget",
  "description": "A useful widget"
}
```

**Not Found** (404):
```json
{
  "detail": "Item with ID 00000000-0000-0000-0000-000000000000 not found."
}
```

## Key Points

- **Validation Happens in the Pipeline**: The `ValidationBehavior` runs all `IValidator<T>` instances before passing to the handler. Invalid requests throw `FluentValidation.ValidationException`.

- **Application Boundary Handles Exceptions**: The global exception handler catches `ValidationException` and maps it to the standard Problem Details format (RFC 7807). This keeps the pipeline pure and the binding between validation and HTTP concerns separated.

- **Handlers Return Plain Values**: Handlers return `ItemDto` directly — no `Result<T>` wrapper. Success is the normal path; exceptions signal failure.

- **Queries Can Throw**: The `GetItemHandler` throws `KeyNotFoundException` when an item is not found, which the endpoint catches and converts to `NotFound()`.
