using LayeredTemplate.Application.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Controllers.V2;

public class UserController : Api.Controllers.V2.UserController
{
    public override Task<ActionResult<CurrentUser>> GetCurrentUser()
    {
        throw new NotImplementedException();
    }
}