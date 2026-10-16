using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Common.Models;

namespace TaskFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.Succeeded) return Ok(result.Value);
        return Failure(result.StatusCode, result.Error);
    }

    protected IActionResult HandleResult(Result result)
    {
        if (result.Succeeded) return NoContent();
        return Failure(result.StatusCode, result.Error);
    }

    private IActionResult Failure(int? statusCode, string? error) => (statusCode ?? 400) switch
    {
        404 => NotFound(new { error }),
        403 => StatusCode(StatusCodes.Status403Forbidden, new { error }),
        401 => Unauthorized(new { error }),
        409 => Conflict(new { error }),
        var code => StatusCode(code, new { error })
    };
}
