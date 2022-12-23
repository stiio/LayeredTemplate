using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Common.Models;
using LayeredTemplate.Domain.Enums;
using LayeredTemplate.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasswordGenerator;

namespace LayeredTemplate.Infrastructure.Services;

internal class UserPoolService : IUserPoolService
{
    private readonly IAmazonCognitoIdentityProvider cognitoIdentityProvider;
    private readonly CognitoSettings cognitoSettings;
    private readonly ILogger<UserPoolService> logger;

    public UserPoolService(
        IAmazonCognitoIdentityProvider cognitoIdentityProvider,
        IOptions<CognitoSettings> cognitoSettings,
        ILogger<UserPoolService> logger)
    {
        this.cognitoIdentityProvider = cognitoIdentityProvider;
        this.cognitoSettings = cognitoSettings.Value;
        this.logger = logger;
    }

    public async Task<Guid> CreateUser(UserPoolCreateUserRequest request)
    {
        var adminCreateUserRequest = new AdminCreateUserRequest()
        {
            Username = request.Email,
            UserPoolId = this.cognitoSettings.UserPoolId,
            DesiredDeliveryMediums = new List<string>()
            {
                "EMAIL",
            },
            UserAttributes = new List<AttributeType>()
            {
                new AttributeType()
                {
                    Name = "email",
                    Value = request.Email,
                },
                new AttributeType()
                {
                    Name = "email_verified",
                    Value = "true",
                },
            },
            MessageAction = request.NotSendEmail ? MessageActionType.SUPPRESS : null,
            ClientMetadata = request.Metadata,
            TemporaryPassword = new Password()
                .IncludeLowercase()
                .IncludeUppercase()
                .IncludeNumeric()
                .IncludeSpecial(@"*!-")
                .LengthRequired(12)
                .Next(),
        };

        if (request.AdditionalProperties?.Count > 0)
        {
            adminCreateUserRequest.UserAttributes.AddRange(request.AdditionalProperties.Select(x => new AttributeType()
            {
                Name = x.Key,
                Value = x.Value,
            }));
        }

        var response = await this.cognitoIdentityProvider.AdminCreateUserAsync(adminCreateUserRequest);

        this.logger.LogInformation("Cognito user created: {userId}", response.User.Username);

        return new Guid(response.User.Username);
    }

    public async Task AddUserToGroup(Guid userId, Role role)
    {
        await this.cognitoIdentityProvider.AdminAddUserToGroupAsync(
            new AdminAddUserToGroupRequest()
            {
                UserPoolId = this.cognitoSettings.UserPoolId,
                Username = userId.ToString(),
                GroupName = role.ToString(),
            });

        this.logger.LogInformation("Cognito user {userId} add to group {group}", userId, role);
    }

    public async Task RemoveUserFromGroup(Guid userId, Role role)
    {
        await this.cognitoIdentityProvider.AdminRemoveUserFromGroupAsync(
            new AdminRemoveUserFromGroupRequest()
            {
                UserPoolId = this.cognitoSettings.UserPoolId,
                Username = userId.ToString(),
                GroupName = role.ToString(),
            });

        this.logger.LogInformation("Cognito user {userId} remove from group {group}", userId, role);
    }
}