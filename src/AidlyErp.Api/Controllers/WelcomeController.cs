using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers;

[ApiController]
[Route("")]
public class WelcomeController : ControllerBase
{
    [HttpGet("")]
    public IActionResult Welcome()
    {
        return Ok(new
        {
            status = "UP",
            message = "Aidly ERP Backend (.NET 10.0) is running successfully",
            version = "1.0.0-MODULAR"
        });
    }
}
