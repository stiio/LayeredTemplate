using MediatR;

namespace LayeredTemplate.Application.ApiKeys.Requests;

public class ApiKeyDeleteRequest : IRequest
{
    public Guid Id { get; set; }
}