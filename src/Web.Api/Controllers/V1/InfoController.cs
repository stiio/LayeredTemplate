using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V1;

[ApiController]
[Route("info")]
public class InfoController : AppControllerBase
{
    [HttpGet("version")]
    public ActionResult<string?> GetVersion()
    {
        return Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    }
}