using LayeredTemplate.Application.Contracts.Models;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests;

/// <inheritdoc />
public class CurrentUserGetRequest : IRequest<CurrentUser>
{
}