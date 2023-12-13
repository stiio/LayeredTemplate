using LayeredTemplate.Application.Features.ApiKeys.Models;
using MediatR;

namespace LayeredTemplate.Application.Features.ApiKeys.Requests;

public class ApiKeySecretGetRequest : IRequest<ApiKeySecretDto>
{
    public Guid Id { get; set; }
}