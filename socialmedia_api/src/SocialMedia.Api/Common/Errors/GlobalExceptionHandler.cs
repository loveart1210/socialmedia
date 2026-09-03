using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Common.Http;

namespace SocialMedia.Api.Common.Errors;

/// <summary>
/// Chỗ duy nhất dịch exception sang response lỗi. Mọi response lỗi của API là
/// ProblemDetails (RFC 7807) — controller/service không tự dựng body lỗi.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var requestId = httpContext.GetRequestId();

        if (exception is AppException appException)
        {
            logger.LogWarning(
                exception,
                "Lỗi nghiệp vụ {StatusCode} tại {Method} {Path} (X-Request-Id: {RequestId})",
                appException.StatusCode,
                httpContext.Request.Method,
                httpContext.Request.Path,
                requestId);

            httpContext.Response.StatusCode = appException.StatusCode;
            return await WriteAsync(httpContext, exception, BuildProblem(appException));
        }

        logger.LogError(
            exception,
            "Lỗi chưa xử lý tại {Method} {Path} (X-Request-Id: {RequestId})",
            httpContext.Request.Method,
            httpContext.Request.Path,
            requestId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await WriteAsync(httpContext, exception, new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Lỗi hệ thống",
            // Không lộ chi tiết exception ra ngoài; tra bằng X-Request-Id trong log.
            Detail = "Có lỗi xảy ra, thử lại sau.",
        });
    }

    private static ProblemDetails BuildProblem(AppException exception)
    {
        if (exception is ValidationFailedException validationFailed)
        {
            return new ValidationProblemDetails(validationFailed.Errors)
            {
                Status = validationFailed.StatusCode,
                Title = validationFailed.Title ?? "Dữ liệu không hợp lệ",
                Detail = validationFailed.Message,
            };
        }

        return new ProblemDetails
        {
            Status = exception.StatusCode,
            Title = exception.Title,
            Detail = exception.Message,
        };
    }

    private ValueTask<bool> WriteAsync(HttpContext httpContext, Exception exception, ProblemDetails problemDetails)
        => problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
}
