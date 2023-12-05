using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests.ApiKeys;

public class ApiKeyDeleteRequest : IRequest
{
    public Guid Id { get; set; }
}