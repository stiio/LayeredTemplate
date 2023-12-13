using LayeredTemplate.Application.Features.ApiKeys.Models;
using MediatR;

namespace LayeredTemplate.Application.Features.ApiKeys.Requests;

public class ApiKeyGetRequest : IRequest<ApiKeyDto>
{
    public Guid Id { get; set; }
}