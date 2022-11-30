using LayeredTemplate.Application.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Mock.Controllers.V1;

public class UserController : Api.Controllers.V1.UserController
{
    public override Task<ActionResult<CurrentUser>> GetCurrentUser()
    {
        return Task.FromResult<ActionResult<CurrentUser>>(new CurrentUser()
        {
            Id = Guid.NewGuid(),
            Email = "example@yopmail.com",
            Name = "FLN",
        });
    }
}