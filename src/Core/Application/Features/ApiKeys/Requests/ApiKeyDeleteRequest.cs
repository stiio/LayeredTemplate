using MediatR;

namespace LayeredTemplate.Application.Features.ApiKeys.Requests;

public class ApiKeyDeleteRequest : IRequest
{
    public Guid Id { get; set; }
}