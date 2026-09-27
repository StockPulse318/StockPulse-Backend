using System.Net;
using System.Text.Json;

namespace StockPulse.API.Middleware;

// Ensures every unhandled exception returns JSON — never an ASP.NET HTML error page.
// Stack traces are only included in Development responses.
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next        = next;
        _logger      = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private async Task WriteErrorResponseAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = (int)HttpStatusCode.InternalServerError;

        var payload = _environment.IsDevelopment()
            ? new { error = ex.Message, detail = ex.StackTrace }
            : new { error = "An unexpected error occurred.", detail = (string?)null };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
