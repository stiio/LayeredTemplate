using LayeredTemplate.App.Application.Features.Users.Models;
using MediatR;

namespace LayeredTemplate.App.Application.Features.Users.Requests;

public class CurrentUserGetRequest : IRequest<CurrentUser>
{
}