using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models.Users;
using LayeredTemplate.Application.Contracts.Requests.Users;
using LayeredTemplate.Web.Api.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V1;

/// <summary>
/// User Controller
/// </summary>
[ApiController]
[Route("users")]
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
    public Task<CurrentUser> GetCurrentUser()
    {
        return this.sender.Send(new CurrentUserGetRequest());
    }

    /// <summary>
    /// Send User Update Email Code
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("email/send_code")]
    public async Task<ActionResult<SuccessfulResult>> SendUserEmailCode([FromForm] UserEmailCodeSendRequest request)
    {
        await this.sender.Send(request);
        return this.SuccessfulResult();
    }

    /// <summary>
    /// Verify User Update Email Code
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPut("email/verify_code")]
    public async Task<ActionResult<SuccessfulResult>> VerifyUserEmailCode([FromForm] UserEmailCodeVerifyRequest request)
    {
        await this.sender.Send(request);
        return this.SuccessfulResult();
    }
}