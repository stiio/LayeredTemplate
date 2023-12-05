using LayeredTemplate.Application.Contracts.Models.ApiKeys;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests.ApiKeys;

public class ApiKeyGetRequest : IRequest<ApiKeyDto>
{
    public Guid Id { get; set; }
}