using Microsoft.AspNetCore.Mvc;
using AidlyErp.Application.Common.Response;

namespace AidlyErp.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult OkResponse<T>(T data, string message = "Operation completed successfully")
    {
        return Ok(ApiResponse<T>.Success(data, message));
    }

    protected IActionResult ErrorResponse(int statusCode, string message)
    {
        return StatusCode(statusCode, ApiResponse<object>.Error(statusCode, message));
    }
}
