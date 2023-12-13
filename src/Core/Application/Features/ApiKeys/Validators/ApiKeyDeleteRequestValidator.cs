using FluentValidation;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.ApiKeys.Requests;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;

namespace LayeredTemplate.Application.Features.ApiKeys.Validators;

internal class ApiKeyDeleteRequestValidator : AbstractValidator<ApiKeyDeleteRequest>
{
    public ApiKeyDeleteRequestValidator(
        IApplicationDbContext context,
        IResourceAuthorizationService resourceAuthorizationService)
    {
        this.RuleFor(x => x.Id)
            .ExistsEntity<ApiKeyDeleteRequest, Guid, ApiKey>(context);

        this.RuleFor(x => x.Id)
            .RequireAccess<ApiKeyDeleteRequest, Guid, ApiKey>(Operations.Delete, context, resourceAuthorizationService);
    }
}