using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Web.Controllers.V1_dev;

[ApiController]
[Route("debug")]
public class DebugController : AppControllerBase
{
    public DebugController()
    {
    }

    [HttpPost("test")]
    public Task<IActionResult> Test()
    {
        return Task.FromResult<IActionResult>(this.Ok());
    }
}