using System.Reflection;
using LayeredTemplate.Application.Contracts.Models.Info;
using LayeredTemplate.Application.Contracts.Requests.Info;
using LayeredTemplate.Shared.Extensions;
using MediatR;

namespace LayeredTemplate.Application.Handlers.Info;

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