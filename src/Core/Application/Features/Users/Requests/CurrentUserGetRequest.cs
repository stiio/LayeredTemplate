using LayeredTemplate.Application.Features.Users.Models;
using MediatR;

namespace LayeredTemplate.Application.Features.Users.Requests;

public class CurrentUserGetRequest : IRequest<CurrentUser>
{
}