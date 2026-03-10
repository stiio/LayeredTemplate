using System.Reflection;
using LayeredTemplate.App.Application.Features.Info.Models;
using LayeredTemplate.App.Application.Features.Info.Requests;
using LayeredTemplate.Shared.Extensions;
using Mediator;

namespace LayeredTemplate.App.Application.Features.Info.Handlers;

internal class InfoGetHandler : IRequestHandler<InfoGetRequest, InfoResponse>
{
    public ValueTask<InfoResponse> Handle(InfoGetRequest request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new InfoResponse()
        {
            BuildDate = Assembly.GetExecutingAssembly().GetBuildDate(),
        });
    }
}