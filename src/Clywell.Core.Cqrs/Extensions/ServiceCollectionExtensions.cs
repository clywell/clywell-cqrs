using Clywell.Core.Cqrs.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clywell.Core.Cqrs.Extensions;

/// <summary>
/// Extension methods for registering Clywell CQRS services with the Microsoft DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Clywell CQRS dispatcher. Call the source-generated
    /// <c>AddCqrsHandlers()</c> method afterwards to register handlers, and
    /// any behavior extension methods for the pipeline behaviors you want.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddCqrs()
    ///         .AddCqrsHandlers();          // source-generated — zero reflection
    /// </code>
    /// </example>
    public static IServiceCollection AddCqrs(this IServiceCollection services)
    {
        services.TryAddTransient<IDispatcher, Dispatcher>();
        return services;
    }
}
