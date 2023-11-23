using LayeredTemplate.Domain.Enums;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Web.Api.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V1;

[ApiController]
[Route("example")]
[RoleAuthorize(Role.Client, AuthenticationSchemes = $"{AppAuthenticationSchemes.Bearer},{AppAuthenticationSchemes.ApiKey}")]
public class ExampleController : AppControllerBase
{
    [HttpGet("auth_type")]
    public IActionResult GetAuthType()
    {
        return this.Ok(this.HttpContext.User.Identity?.AuthenticationType);
    }
}