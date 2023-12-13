using LayeredTemplate.Application.ApiKeys.Models;
using MediatR;

namespace LayeredTemplate.Application.ApiKeys.Requests;

public class ApiKeySecretGetRequest : IRequest<ApiKeySecretDto>
{
    public Guid Id { get; set; }
}