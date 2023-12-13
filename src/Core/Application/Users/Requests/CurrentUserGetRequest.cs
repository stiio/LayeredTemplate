using LayeredTemplate.Application.Users.Models;
using MediatR;

namespace LayeredTemplate.Application.Users.Requests;

public class CurrentUserGetRequest : IRequest<CurrentUser>
{
}