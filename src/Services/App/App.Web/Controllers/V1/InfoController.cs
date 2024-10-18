using System.Reflection;
using LayeredTemplate.App.Application.Features.Info.Models;
using LayeredTemplate.App.Application.Features.Info.Requests;
using LayeredTemplate.Shared.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Web.Controllers.V1;

[ApiController]
[Route("info")]
public class InfoController : AppControllerBase
{
    [HttpGet]
    public async Task<InfoResponse> GetInfo()
    {
        var response = await this.Sender.Send(new InfoGetRequest());
        response.NpmPackageVersion = Assembly.GetExecutingAssembly().GetVersion();
        return response;
    }
}