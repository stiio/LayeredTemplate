using LayeredTemplate.Application.ApiKeys.Models;
using LayeredTemplate.Application.ApiKeys.Requests;
using LayeredTemplate.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Controllers.V1;

[ApiController]
[Route("api_keys")]
[Authorize]
public class ApiKeyController : AppControllerBase
{
    [HttpGet]
    public Task<ApiKeyDto[]> ListApiKey()
    {
        return this.Sender.Send(new ApiKeyListRequest());
    }

    [HttpPost]
    public Task<ApiKeySecretDto> CreateApiKey(ApiKeyCreateRequest request)
    {
        return this.Sender.Send(request);
    }

    [HttpGet("{id}")]
    public Task<ApiKeyDto> GetApiKey(ApiKeyGetRequest request)
    {
        return this.Sender.Send(request);
    }

    [HttpPut("{id}")]
    public Task<ApiKeyDto> UpdateApiKey(ApiKeyUpdateRequest request)
    {
        return this.Sender.Send(request);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<SuccessfulResult>> DeleteApiKey(ApiKeyDeleteRequest request)
    {
        await this.Sender.Send(request);
        return this.SuccessfulResult();
    }

    [HttpGet("{id}/secret")]
    public Task<ApiKeySecretDto> GetApiKeySecret(ApiKeySecretGetRequest request)
    {
        return this.Sender.Send(request);
    }
}