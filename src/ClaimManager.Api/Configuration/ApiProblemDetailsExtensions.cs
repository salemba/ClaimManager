namespace ClaimManager.Api.Configuration;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public static class ApiProblemDetailsExtensions
{
    public static async Task WriteProblemDetailsAsync(StatusCodeContext context)
    {
        if (!context.HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            return;
        }

        var statusCode = context.HttpContext.Response.StatusCode;
        if (statusCode is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
        {
            return;
        }

        var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode == StatusCodes.Status401Unauthorized
                    ? "Authentication required"
                    : "Insufficient permissions",
                Detail = statusCode == StatusCodes.Status401Unauthorized
                    ? "The requested API endpoint requires an authenticated user."
                    : "The current user is not authorized to access this API endpoint."
            }
        });
    }
}