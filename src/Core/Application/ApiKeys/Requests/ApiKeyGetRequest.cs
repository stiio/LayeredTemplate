using LayeredTemplate.Application.ApiKeys.Models;
using MediatR;

namespace LayeredTemplate.Application.ApiKeys.Requests;

public class ApiKeyGetRequest : IRequest<ApiKeyDto>
{
    public Guid Id { get; set; }
}