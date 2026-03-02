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
    /// <see cref="AddCqrsBehavior{TBehavior}"/> for any pipeline behaviors you want.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddCqrs()
    ///         .AddCqrsHandlers()                              // source-generated — zero reflection
    ///         .AddCqrsBehavior&lt;MyErrorHandlingBehavior&lt;,&gt;&gt;()  // open-generic behavior
    ///         .AddCqrsFluentValidation(typeof(MyValidator).Assembly);
    /// </code>
    /// </example>
    public static IServiceCollection AddCqrs(this IServiceCollection services)
    {
        services.TryAddTransient<IDispatcher, Dispatcher>();
        return services;
    }

    /// <summary>
    /// Registers an open-generic <see cref="IPipelineBehavior{TRequest,TResult}"/> implementation
    /// that will run for every request dispatched through the pipeline.
    /// </summary>
    /// <typeparam name="TBehavior">
    /// An open-generic type implementing <see cref="IPipelineBehavior{TRequest,TResult}"/>.
    /// Pass the open generic form, e.g. <c>typeof(MyBehavior&lt;,&gt;)</c>.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// Behaviors run in registration order — the first registered behavior is outermost
    /// in the pipeline (runs before all others). Register error-handling behaviors before
    /// logging behaviors if you want errors caught before they reach the logger.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Custom error-handling behavior (maps exceptions to Result failures):
    /// public sealed class MyErrorBehavior&lt;TRequest, TResult&gt;
    ///     : ErrorHandlingBehavior&lt;TRequest, TResult&gt;
    ///     where TRequest : notnull
    /// {
    ///     protected override Task&lt;TResult&gt; HandleExceptionAsync(
    ///         TRequest request, Exception ex, CancellationToken ct)
    ///     {
    ///         // map, log, or rethrow — your choice
    ///         throw new MyDomainException(ex.Message, ex);
    ///     }
    /// }
    ///
    /// services.AddCqrs()
    ///         .AddCqrsHandlers()
    ///         .AddCqrsBehavior&lt;MyErrorBehavior&lt;,&gt;&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddCqrsBehavior<TBehavior>(this IServiceCollection services)
        where TBehavior : class
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TBehavior));
        return services;
    }
}
