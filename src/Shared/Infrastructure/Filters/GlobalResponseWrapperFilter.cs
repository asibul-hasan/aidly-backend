using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Aidly.src.Shared.Domain.Wrappers;

namespace Aidly.src.Shared.Infrastructure.Filters;

public class GlobalResponseWrapperFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult objectResult)
        {
            var type = objectResult.Value?.GetType();

            // جلوگیری double wrapping
            if (type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiResponse<>))
            {
                await next();
                return;
            }

            var statusCode = objectResult.StatusCode ?? context.HttpContext.Response.StatusCode;

            if (statusCode >= 200 && statusCode < 300)
            {
                objectResult.Value = ApiResponse<object>.CreateSuccess(
                    data: objectResult.Value,
                    statusCode: statusCode
                );
            }
            else
            {
                object? errors = objectResult.Value;
                string message = "Operation failed.";

                if (objectResult.Value is ValidationProblemDetails validationProblem)
                {
                    // Flatten dictionary into a single line string for toaster UI
                    var errorMessages = validationProblem.Errors.SelectMany(kvp => kvp.Value);
                    errors = string.Join(" ", errorMessages);
                    message = validationProblem.Title ?? message;
                }
                else if (objectResult.Value is ProblemDetails problemDetails)
                {
                    errors = problemDetails.Detail;
                    message = problemDetails.Title ?? message;
                }

                objectResult.Value = ApiResponse<object>.CreateError(
                    message: message,
                    errors: errors,
                    statusCode: statusCode
                );
            }
        }

        await next();
    }
}
