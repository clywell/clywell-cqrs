using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Clywell.Core.Cqrs.Sample;

/// <summary>
/// Extension methods for registering the global exception handler and error responses.
/// </summary>
internal static class ErrorHandlingExtensions
{
    internal static WebApplication MapGlobalExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                context.Response.ContentType = "application/json";

                var endpoint = context.GetEndpoint();
                var feature = context.Features.Get<IExceptionHandlerPathFeature>();
                var exception = feature?.Error;

                if (exception is ValidationException validationException)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;

                    await context.Response.WriteAsJsonAsync(new
                    {
                        type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                        title = "One or more validation errors occurred.",
                        status = 400,
                        errors = validationException.Errors
                            .GroupBy(e => e.PropertyName)
                            .ToDictionary(
                                g => g.Key,
                                g => g.Select(e => e.ErrorMessage).ToArray())
                    });
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    await context.Response.WriteAsJsonAsync(new
                    {
                        type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                        title = "An error occurred processing your request.",
                        status = 500,
                        detail = exception?.Message
                    });
                }
            });
        });

        return app;
    }
}
