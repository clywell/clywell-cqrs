using FluentValidation;
using ValidationFailure = FluentValidation.Results.ValidationFailure;

namespace Clywell.Core.Cqrs.FluentValidation;

/// <summary>
/// Pipeline behavior that runs all registered <see cref="IValidator{T}"/> instances for the
/// incoming request before passing control to the next step. Throws
/// <see cref="ValidationException"/> (FluentValidation's own) if any validator fails, so the
/// application boundary (exception filter, minimal-API error handler, etc.) can map the
/// failures to the appropriate response format.
/// </summary>
/// <typeparam name="TRequest">The command or query type being validated.</typeparam>
/// <typeparam name="TResult">The result type produced by the handler.</typeparam>
/// <remarks>
/// Register this behavior via
/// <see cref="Extensions.ServiceCollectionExtensions.AddCqrsFluentValidation"/>.
/// If no <see cref="IValidator{T}"/> is registered for a given request type the behavior is
/// a no-op and execution continues normally.
/// </remarks>
/// <remarks>
/// Initializes a new instance of <see cref="ValidationBehavior{TRequest,TResult}"/>.
/// </remarks>
/// <param name="validators">
/// All <see cref="IValidator{T}"/> implementations registered for
/// <typeparamref name="TRequest"/>. May be empty when no validator exists for the request.
/// </param>
public sealed class ValidationBehavior<TRequest, TResult>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResult>
    where TRequest : notnull
{

    /// <inheritdoc/>
    public async Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken ct = default)
    {
        if (!validators.Any())
        {
            return await next(ct);
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, ct);
            failures.AddRange(result.Errors.Where(f => f is not null));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(ct);
    }
}
