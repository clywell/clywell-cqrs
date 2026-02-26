using Clywell.Core.Cqrs;
using Clywell.Core.Cqrs.Extensions;
using Clywell.Core.Cqrs.FluentValidation.Extensions;
using Clywell.Core.Cqrs.Sample;
using Clywell.Core.Cqrs.Sample.Features.Items;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// Add CQRS and FluentValidation Pipeline to DI Container
// ============================================================================

builder.Services
    .AddCqrs()
    .AddCqrsHandlers()                                         // source-generated — zero reflection
    .AddCqrsFluentValidation(typeof(CreateItemCommandValidator).Assembly);  // scan for validators

builder.Services.AddOpenApi();

var app = builder.Build();

// ============================================================================
// Global Exception Handler
// ============================================================================

app.MapGlobalExceptionHandler();

// ============================================================================
// OpenAPI / Scalar Documentation
// ============================================================================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// ============================================================================
// CQRS Endpoints
// ============================================================================

var itemsGroup = app.MapGroup("/api/items")
    .WithName("Items");

itemsGroup
    .MapPost("/", CreateItem)
    .WithName("Create Item")
    .WithDescription("Creates a new item with the given name and description. Validates input via FluentValidation.");

itemsGroup
    .MapGet("/{id:guid}", GetItem)
    .WithName("Get Item")
    .WithDescription("Retrieves an item by ID.");

app.Run();

// ============================================================================
// Endpoint Handlers
// ============================================================================

static async Task<IResult> CreateItem(IDispatcher dispatcher, CreateItemRequest request, CancellationToken ct)
{
    var command = new CreateItemCommand(request.Name, request.Description);
    var result = await dispatcher.SendAsync(command, ct);
    return Results.Created($"/api/items/{result.Id}", result);
}

static async Task<IResult> GetItem(IDispatcher dispatcher, Guid id, CancellationToken ct)
{
    try
    {
        var query = new GetItemQuery(id);
        var result = await dispatcher.QueryAsync(query, ct);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
}

// ============================================================================
// Request/Response DTOs
// ============================================================================

internal record CreateItemRequest(string Name, string Description);
