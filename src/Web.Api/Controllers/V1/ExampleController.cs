using LayeredTemplate.Domain.Enums;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Web.Api.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V1;

/// <summary>
/// Example Controller
/// </summary>
[ApiController]
[Route("example")]
[RoleAuthorize(Role.Client, AuthenticationSchemes = $"{AppAuthenticationSchemes.User},{AppAuthenticationSchemes.ApiKey}")]
public class ExampleController : AppControllerBase
{
    /// <summary>
    /// Get Auth Type
    /// </summary>
    /// <returns></returns>
    [HttpGet("auth_type")]
    public IActionResult GetAuthType()
    {
        return this.Ok(this.HttpContext.User.Identity?.AuthenticationType);
    }
}