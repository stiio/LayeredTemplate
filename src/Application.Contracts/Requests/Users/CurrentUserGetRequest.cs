using LayeredTemplate.Application.Contracts.Models.Users;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests.Users;

/// <inheritdoc />
public class CurrentUserGetRequest : IRequest<CurrentUser>
{
}