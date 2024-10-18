using LayeredTemplate.App.Application.Features.Users.Models;
using LayeredTemplate.App.Application.Features.Users.Requests;
using LayeredTemplate.App.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Web.Controllers.V1;

[ApiController]
[Route("users")]
[Authorize]
public class UserController : AppControllerBase
{
    [HttpGet("current_user")]
    public Task<CurrentUser> GetCurrentUser()
    {
        return this.Sender.Send(new CurrentUserGetRequest());
    }

    [HttpPost("email/send_code")]
    public async Task<ActionResult<SuccessfulResult>> SendUserEmailCode([FromBody] UserEmailCodeSendRequest request)
    {
        await this.Sender.Send(request);
        return this.SuccessfulResult();
    }

    [HttpPut("email/verify_code")]
    public async Task<ActionResult<SuccessfulResult>> VerifyUserEmailCode([FromBody] UserEmailCodeVerifyRequest request)
    {
        await this.Sender.Send(request);
        return this.SuccessfulResult();
    }
}