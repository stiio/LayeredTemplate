using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V1;

/// <summary>
/// User Controller
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UserController : AppControllerBase
{
    private readonly ISender sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserController"/> class.
    /// </summary>
    /// <param name="sender"></param>
    public UserController(ISender sender)
    {
        this.sender = sender;
    }

    /// <summary>
    /// Get Current User
    /// </summary>
    /// <returns>Return <see cref="CurrentUser"/></returns>
    [HttpGet("current_user")]
    public async Task<ActionResult<CurrentUser>> GetCurrentUser()
    {
        return await this.sender.Send(new CurrentUserGetRequest());
    }
}