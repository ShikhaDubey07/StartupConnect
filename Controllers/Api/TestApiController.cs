using Microsoft.AspNetCore.Mvc;

namespace StartupConnect.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class TestApiController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            message = "StartupConnect API is working!"
        });
    }
}