using LayeredTemplate.Application.Contracts.Models.Users;
using MediatR;

namespace LayeredTemplate.Application.Contracts.Requests.Users;

public class CurrentUserGetRequest : IRequest<CurrentUser>
{
}