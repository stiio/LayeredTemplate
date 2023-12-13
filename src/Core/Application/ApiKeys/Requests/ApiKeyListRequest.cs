using LayeredTemplate.Application.ApiKeys.Models;
using MediatR;

namespace LayeredTemplate.Application.ApiKeys.Requests;

public class ApiKeyListRequest : IRequest<ApiKeyDto[]>
{
}