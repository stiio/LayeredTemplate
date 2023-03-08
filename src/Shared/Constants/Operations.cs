using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace LayeredTemplate.Shared.Constants;

public static class Operations
{
    public static OperationAuthorizationRequirement Read => new() { Name = nameof(Read) };

    public static OperationAuthorizationRequirement Update => new() { Name = nameof(Update) };

    public static OperationAuthorizationRequirement Delete => new() { Name = nameof(Delete) };
}