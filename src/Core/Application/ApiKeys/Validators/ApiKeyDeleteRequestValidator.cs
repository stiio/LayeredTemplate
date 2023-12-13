using FluentValidation;
using LayeredTemplate.Application.ApiKeys.Requests;
using LayeredTemplate.Application.Common.Extensions;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Shared.Constants;

namespace LayeredTemplate.Application.ApiKeys.Validators;

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