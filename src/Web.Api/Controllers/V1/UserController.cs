using LayeredTemplate.Application.Contracts.Models.Users;
using LayeredTemplate.Application.Contracts.Requests.Users;
using LayeredTemplate.Web.Api.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V1;

[ApiController]
[Route("users")]
[Authorize]
public class UserController : AppControllerBase
{
    private readonly ISender sender;

    public UserController(ISender sender)
    {
        this.sender = sender;
    }

    [HttpGet("current_user")]
    public Task<CurrentUser> GetCurrentUser()
    {
        return this.sender.Send(new CurrentUserGetRequest());
    }

    [HttpPost("email/send_code")]
    public async Task<ActionResult<SuccessfulResult>> SendUserEmailCode([FromForm] UserEmailCodeSendRequest request)
    {
        await this.sender.Send(request);
        return this.SuccessfulResult();
    }

    [HttpPut("email/verify_code")]
    public async Task<ActionResult<SuccessfulResult>> VerifyUserEmailCode([FromForm] UserEmailCodeVerifyRequest request)
    {
        await this.sender.Send(request);
        return this.SuccessfulResult();
    }
}