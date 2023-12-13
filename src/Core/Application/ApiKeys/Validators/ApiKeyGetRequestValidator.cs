using FluentValidation;
using LayeredTemplate.Application.ApiKeys.Requests;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;

namespace LayeredTemplate.Application.ApiKeys.Validators;

internal class ApiKeyGetRequestValidator : AbstractValidator<ApiKeyGetRequest>
{
    public ApiKeyGetRequestValidator(
        IApplicationDbContext dbContext,
        IResourceAuthorizationService resourceAuthorizationService)
    {
        this.RuleFor(x => x.Id)
            .ExistsEntity<ApiKeyGetRequest, Guid, ApiKey>(dbContext);

        this.RuleFor(x => x.Id)
            .RequireAccess<ApiKeyGetRequest, Guid, ApiKey>(Operations.Read, dbContext, resourceAuthorizationService);
    }
}