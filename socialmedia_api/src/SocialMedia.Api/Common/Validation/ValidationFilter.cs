using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;
using SocialMedia.Api.Common.Errors;

namespace SocialMedia.Api.Common.Validation;

/// <summary>
/// Chạy validator FluentValidation của mọi tham số action trước khi vào controller.
/// Validator đăng ký qua <c>AddValidatorsFromAssembly</c>; lỗi ném ra dưới dạng
/// <see cref="ValidationFailedException"/> để middleware trả ProblemDetails 400.
/// </summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            if (services.GetService(typeof(IValidator<>).MakeGenericType(argument.GetType())) is not IValidator validator)
            {
                continue;
            }

            ValidationResult result = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                throw new ValidationFailedException(result.ToDictionary());
            }
        }

        await next();
    }
}
