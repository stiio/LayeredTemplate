using LayeredTemplate.Application.Contracts.Models.Info;
using LayeredTemplate.Application.Contracts.Requests.Info;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers.V1;

[ApiController]
[Route("info")]
public class InfoController : AppControllerBase
{
    private readonly ISender sender;

    public InfoController(ISender sender)
    {
        this.sender = sender;
    }

    [HttpGet]
    public Task<InfoResponse> GetInfo()
    {
        return this.sender.Send(new InfoGetRequest());
    }
}