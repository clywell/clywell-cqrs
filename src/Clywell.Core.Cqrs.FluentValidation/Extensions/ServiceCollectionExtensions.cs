using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Clywell.Core.Cqrs.FluentValidation.Extensions;

/// <summary>
/// Extension methods for registering the FluentValidation pipeline behavior with the Microsoft
/// DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="ValidationBehavior{TRequest,TResult}"/> pipeline behavior and
    /// scans the supplied <paramref name="assemblies"/> for all
    /// <see cref="IValidator{T}"/> implementations, registering them as scoped services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">
    /// Assemblies to scan for <see cref="IValidator{T}"/> implementations. Pass at least the
    /// assembly that contains your validators, e.g.
    /// <c>typeof(CreateItemCommandValidator).Assembly</c>.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddCqrs()
    ///         .AddCqrsHandlers()           // source-generated — zero reflection
    ///         .AddCqrsFluentValidation(typeof(CreateItemCommandValidator).Assembly);
    /// </code>
    /// </example>
    public static IServiceCollection AddCqrsFluentValidation(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        services.AddValidatorsFromAssemblies(assemblies);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }
}
