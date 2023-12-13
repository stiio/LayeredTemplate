using System.Reflection;
using LayeredTemplate.Application.Info.Models;
using LayeredTemplate.Application.Info.Requests;
using LayeredTemplate.Shared.Extensions;
using MediatR;

namespace LayeredTemplate.Application.Info.Handlers;

internal class InfoGetHandler : IRequestHandler<InfoGetRequest, InfoResponse>
{
    public Task<InfoResponse> Handle(InfoGetRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new InfoResponse()
        {
            BuildDate = Assembly.GetExecutingAssembly().GetBuildDate(),
        });
    }
}