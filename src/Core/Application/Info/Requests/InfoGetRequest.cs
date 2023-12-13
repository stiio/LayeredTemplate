using LayeredTemplate.Application.Info.Models;
using MediatR;

namespace LayeredTemplate.Application.Info.Requests;

public class InfoGetRequest : IRequest<InfoResponse>
{
}