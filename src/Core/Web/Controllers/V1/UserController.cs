﻿using LayeredTemplate.Application.Users.Models;
using LayeredTemplate.Application.Users.Requests;
using LayeredTemplate.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Controllers.V1;

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
    public async Task<ActionResult<SuccessfulResult>> SendUserEmailCode([FromForm] UserEmailCodeSendRequest request)
    {
        await this.Sender.Send(request);
        return this.SuccessfulResult();
    }

    [HttpPut("email/verify_code")]
    public async Task<ActionResult<SuccessfulResult>> VerifyUserEmailCode([FromForm] UserEmailCodeVerifyRequest request)
    {
        await this.Sender.Send(request);
        return this.SuccessfulResult();
    }
}