namespace Kreator_API.Middleware;

public class CsrfMiddleware
{
    private readonly RequestDelegate _next;

    public CsrfMiddleware(
        RequestDelegate next
    )
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context
    )
    {
        string method =
            context.Request.Method;

        if (
            method == "POST" ||
            method == "PUT" ||
            method == "PATCH" ||
            method == "DELETE"
        )
        {
            string? cookieToken =
                context.Request
                    .Cookies["csrfToken"];

            string? headerToken =
                context.Request
                    .Headers["X-CSRF-TOKEN"];

            if (
                string.IsNullOrWhiteSpace(
                    cookieToken
                ) ||
                string.IsNullOrWhiteSpace(
                    headerToken
                ) ||
                cookieToken != headerToken
            )
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                await context.Response.WriteAsJsonAsync(
                    new
                    {
                        message =
                            "Invalid CSRF token"
                    }
                );

                return;
            }
        }

        await _next(context);
    }
}