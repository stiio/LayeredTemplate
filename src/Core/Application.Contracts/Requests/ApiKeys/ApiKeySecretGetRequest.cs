using LayeredTemplate.Application.Contracts.Models.ApiKeys;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests.ApiKeys;

public class ApiKeySecretGetRequest : IRequest<ApiKeySecretDto>
{
    public Guid Id { get; set; }
}