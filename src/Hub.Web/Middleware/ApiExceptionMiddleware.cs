using Hub.Domain.Common;

namespace Hub.Web.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (UseCaseException e)
        {
            var status = e.Kind switch
            {
                ErrorKind.Validation => 400,
                ErrorKind.NotFound => 404,
                ErrorKind.Conflict => 409,
                ErrorKind.Unauthorized => 401,
                ErrorKind.Forbidden => 403,
                ErrorKind.Unavailable => 502,
                ErrorKind.RateLimited => 429,
                _ => 500
            };
            await Error(context, status, e.Message);
        }
        catch (DomainRuleException e) { await Error(context, e.IsConflict ? 409 : 400, e.Message); }
        catch (UnauthorizedAccessException e) { await Error(context, 403, e.Message); }
        catch (ArgumentException e) { await Error(context, 400, e.Message); }
        catch (Exception e)
        {
            logger.LogError("Request {Path} failed with {Type}", context.Request.Path, e.GetType().Name);
            await Error(context, 500, "The request could not be completed. Please retry.");
        }
    }

    private static async Task Error(HttpContext context, int status, string message)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { error = message });
    }
}
