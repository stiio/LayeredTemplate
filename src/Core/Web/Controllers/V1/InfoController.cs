﻿using LayeredTemplate.Application.Features.Info.Models;
using LayeredTemplate.Application.Features.Info.Requests;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Controllers.V1;

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