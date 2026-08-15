using System.Net;
using Microsoft.AspNetCore.Mvc;
using PortYard.Domain.Exceptions;

namespace PortYard.Api.Middleware;

/// <summary>
/// Gives the whole API one consistent error shape: <see cref="DomainRuleException"/> maps to
/// 409 Conflict, <see cref="EntityNotFoundException"/> to 404 Not Found, and anything unexpected
/// to 500. Model-validation failures never reach this middleware — ASP.NET Core's [ApiController]
/// model binding already returns 400 with a ValidationProblemDetails body of the same shape.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainRuleException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "Business rule violated", ex.Message);
        }
        catch (EntityNotFoundException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.NotFound, "Not found", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred",
                "An unexpected error occurred while processing the request.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, HttpStatusCode statusCode, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsJsonAsync(problem);
    }
}
