using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            RuleViolation => (400, exception.Message),
            VersionConflict or DbUpdateConcurrencyException => (409, "Заявка уже изменена. Обновите данные и повторите действие."),
            BadHttpRequestException => (400, "Проверьте формат запроса."),
            _ => (500, "Не удалось выполнить запрос. Попробуйте ещё раз.")
        };
        if (status == 500) logger.LogError(exception, "Unhandled API error {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status, Title = title, Extensions = { ["traceId"] = context.TraceIdentifier }
        }, cancellationToken: ct);
        return true;
    }
}
