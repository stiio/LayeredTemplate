using System.Reflection;
using LayeredTemplate.App.Application.Features.Info.Models;
using LayeredTemplate.App.Application.Features.Info.Requests;
using LayeredTemplate.Shared.Extensions;
using MediatR;

namespace LayeredTemplate.App.Application.Features.Info.Handlers;

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