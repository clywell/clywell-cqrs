using Clywell.Core.Cqrs.Extensions;
using Clywell.Core.Cqrs.FluentValidation.Extensions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Clywell.Core.Cqrs.FluentValidation.Tests;

// ---------------------------------------------------------------------------
// Test fixtures
// ---------------------------------------------------------------------------

public record CreateItemCommand(string Name) : ICommand<string>;
public record DeleteItemCommand(Guid Id) : ICommand<bool>;

public class CreateItemHandler : ICommandHandler<CreateItemCommand, string>
{
    public Task<string> HandleAsync(CreateItemCommand command, CancellationToken ct = default)
        => Task.FromResult($"Created:{command.Name}");
}

public class DeleteItemHandler : ICommandHandler<DeleteItemCommand, bool>
{
    public Task<bool> HandleAsync(DeleteItemCommand command, CancellationToken ct = default)
        => Task.FromResult(true);
}

public class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(x => x.Name).MaximumLength(50).WithMessage("Name must not exceed 50 characters.");
    }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

file static class ServiceProviderFactory
{
    internal static ServiceProvider BuildWithValidation(bool registerValidator = true)
    {
        var services = new ServiceCollection();
        services.AddCqrs();
        services.AddCqrsHandlers(); // source-generated

        if (registerValidator)
        {
            services.AddCqrsFluentValidation(typeof(CreateItemCommandValidator).Assembly);
        }

        return services.BuildServiceProvider();
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class ValidationBehaviorTests
{
    [Fact]
    public async Task ValidCommand_PassesThrough_HandlerReturnsResult()
    {
        await using var sp = ServiceProviderFactory.BuildWithValidation();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync(new CreateItemCommand("Widget"), default);

        Assert.Equal("Created:Widget", result);
    }

    [Fact]
    public async Task InvalidCommand_EmptyName_ThrowsValidationException()
    {
        await using var sp = ServiceProviderFactory.BuildWithValidation();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.SendAsync(new CreateItemCommand(string.Empty), default));

        Assert.Contains(ex.Errors, e => e.PropertyName == "Name" && e.ErrorMessage == "Name is required.");
    }

    [Fact]
    public async Task InvalidCommand_NameTooLong_ThrowsValidationException()
    {
        await using var sp = ServiceProviderFactory.BuildWithValidation();
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var longName = new string('x', 51);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.SendAsync(new CreateItemCommand(longName), default));

        Assert.Contains(ex.Errors, e => e.PropertyName == "Name" && e.ErrorMessage == "Name must not exceed 50 characters.");
    }

    [Fact]
    public async Task InvalidCommand_MultipleRulesBroken_AllErrorsReported()
    {
        await using var sp = ServiceProviderFactory.BuildWithValidation();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // Empty string violates NotEmpty; length is 0 which is ≤ 50 so only one failure here.
        // Use a null-like value that fails both: send a 51-char string that is also whitespace.
        var longWhitespace = new string(' ', 51);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.SendAsync(new CreateItemCommand(longWhitespace), default));

        // NotEmpty treats whitespace as empty; MaximumLength(50) is also exceeded.
        Assert.True(ex.Errors.Count() >= 2);
    }

    [Fact]
    public async Task CommandWithNoValidatorRegistered_PassesThrough_HandlerReturnsResult()
    {
        // DeleteItemCommand has no validator — behavior should be a no-op.
        await using var sp = ServiceProviderFactory.BuildWithValidation();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync(new DeleteItemCommand(Guid.NewGuid()), default);

        Assert.True(result);
    }

    [Fact]
    public async Task NoBehaviorRegistered_CommandPassesThrough()
    {
        // No AddCqrsFluentValidation call — validation behavior is absent entirely.
        var services = new ServiceCollection();
        services.AddCqrs();
        services.AddCqrsHandlers();
        await using var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // Even an "invalid" command goes straight to the handler.
        var result = await dispatcher.SendAsync(new CreateItemCommand(string.Empty), default);

        Assert.Equal("Created:", result);
    }
}
