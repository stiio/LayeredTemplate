using FluentValidation;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Contracts.Requests.ApiKeys;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;

namespace LayeredTemplate.Application.Validators.ApiKeys;

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