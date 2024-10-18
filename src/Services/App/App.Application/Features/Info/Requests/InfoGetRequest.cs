using LayeredTemplate.App.Application.Features.Info.Models;
using MediatR;

namespace LayeredTemplate.App.Application.Features.Info.Requests;

public class InfoGetRequest : IRequest<InfoResponse>
{
}