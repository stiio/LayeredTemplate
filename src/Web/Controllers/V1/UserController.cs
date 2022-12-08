using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Application.Contracts.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Controllers.V1;

public class UserController : Api.Controllers.V1.UserController
{
    private readonly ISender sender;

    public UserController(ISender sender)
    {
        this.sender = sender;
    }

    public override async Task<ActionResult<CurrentUser>> GetCurrentUser()
    {
        return await this.sender.Send(new CurrentUserGetRequest());
    }
}