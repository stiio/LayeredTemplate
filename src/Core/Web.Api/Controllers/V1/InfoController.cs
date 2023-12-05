using LayeredTemplate.Application.Contracts.Models.Info;
using LayeredTemplate.Application.Contracts.Requests.Info;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V1;

[ApiController]
[Route("info")]
public class InfoController : AppControllerBase
{
    [HttpGet]
    public Task<InfoResponse> GetInfo()
    {
        return this.Sender.Send(new InfoGetRequest());
    }
}