using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace LayeredTemplate.Shared.Constants;

public static class Operations
{
    public static OperationAuthorizationRequirement FullAccess => new() { Name = nameof(FullAccess) };
}