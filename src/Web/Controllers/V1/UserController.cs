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

    public override Task<ActionResult<CurrentUser>> GetCurrentUser()
    {
        // TODO: Mock
        // return await this.sender.Send(new CurrentUserGetRequest());
        return Task.FromResult<ActionResult<CurrentUser>>(new CurrentUser()
        {
            Id = new Guid("682758E0-86A9-47EB-B772-A620095D5C0E"),
            Email = "example@email.com",
            Name = "FLN",
        });
    }
}