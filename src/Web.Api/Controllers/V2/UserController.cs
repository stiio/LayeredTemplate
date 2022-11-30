using LayeredTemplate.Application.Contracts.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V2;

/// <summary>
/// User Controller
/// </summary>
[ApiController]
[Route("api/v2/users")]
[Authorize]
public abstract class UserController : AppControllerBase
{
    /// <summary>
    /// Get Current User
    /// </summary>
    /// <returns>Return <see cref="CurrentUser"/></returns>
    [HttpGet("current_user")]
    public abstract Task<ActionResult<CurrentUser>> GetCurrentUser();
}