using LayeredTemplate.Application.Contracts.Models.ApiKeys;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests.ApiKeys;

public class ApiKeyListRequest : IRequest<ApiKeyDto[]>
{
}