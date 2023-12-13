using FluentValidation;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.ApiKeys.Requests;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;

namespace LayeredTemplate.Application.Features.ApiKeys.Validators;

internal class ApiKeySecretGetRequestValidator : AbstractValidator<ApiKeySecretGetRequest>
{
    public ApiKeySecretGetRequestValidator(
        IApplicationDbContext dbContext,
        IResourceAuthorizationService resourceAuthorizationService)
    {
        this.RuleFor(x => x.Id)
            .ExistsEntity<ApiKeySecretGetRequest, Guid, ApiKey>(dbContext);

        this.RuleFor(x => x.Id)
            .RequireAccess<ApiKeySecretGetRequest, Guid, ApiKey>(Operations.Read, dbContext, resourceAuthorizationService);
    }
}