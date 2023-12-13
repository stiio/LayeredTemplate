using FluentValidation;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.ApiKeys.Requests;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;

namespace LayeredTemplate.Application.Features.ApiKeys.Validators;

internal class ApiKeyUpdateRequestValidator : AbstractValidator<ApiKeyUpdateRequest>
{
    public ApiKeyUpdateRequestValidator(
        IApplicationDbContext dbContext,
        IResourceAuthorizationService resourceAuthorizationService)
    {
        this.RuleFor(x => x.Id)
            .ExistsEntity<ApiKeyUpdateRequest, Guid, ApiKey>(dbContext);

        this.RuleFor(x => x.Id)
            .RequireAccess<ApiKeyUpdateRequest, Guid, ApiKey>(Operations.Update, dbContext, resourceAuthorizationService);
    }
}