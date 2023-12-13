using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using LayeredTemplate.Application.Users.Models;
using LayeredTemplate.Application.Users.Services;
using LayeredTemplate.Domain.Enums;
using LayeredTemplate.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasswordGenerator;

namespace LayeredTemplate.Infrastructure.Services.Users;

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
                new AttributeType()
                {
                    Name = "phone_number",
                    Value = request.Phone ?? string.Empty,
                },
                new AttributeType()
                {
                    Name = "phone_number_verified",
                    Value = (!string.IsNullOrEmpty(request.Phone)).ToString().ToLower(),
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

        var userId = new Guid(response.User.Username);

        await this.AddUserToGroup(userId, request.Role);

        return userId;
    }

    public async Task UpdateUserProperties(UserPoolUpdateUserRequest request)
    {
        var currentRole = await this.GetUserRole(request.Id);

        if (currentRole != request.Role)
        {
            await this.RemoveUserFromGroup(request.Id, currentRole);
            await this.AddUserToGroup(request.Id, request.Role);
        }

        var adminUpdateUserRequest = new AdminUpdateUserAttributesRequest()
        {
            UserPoolId = this.cognitoSettings.UserPoolId,
            Username = request.Id.ToString(),
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
                new AttributeType()
                {
                    Name = "phone_number",
                    Value = request.Phone ?? string.Empty,
                },
                new AttributeType()
                {
                    Name = "phone_number_verified",
                    Value = (!string.IsNullOrEmpty(request.Phone)).ToString().ToLower(),
                },
            },
        };

        await this.cognitoIdentityProvider.AdminUpdateUserAttributesAsync(adminUpdateUserRequest);

        this.logger.LogInformation("Cognito user {useId} updated attributes {@request}", request.Id, adminUpdateUserRequest);
    }

    public async Task<bool> ExistsUser(Guid userId)
    {
        try
        {
            var response = await this.cognitoIdentityProvider.AdminGetUserAsync(new AdminGetUserRequest()
            {
                UserPoolId = this.cognitoSettings.UserPoolId,
                Username = userId.ToString(),
            });

            return true;
        }
        catch (UserNotFoundException)
        {
            return false;
        }
    }

    public async Task DeleteUser(Guid userId)
    {
        await this.cognitoIdentityProvider.AdminDeleteUserAsync(new AdminDeleteUserRequest()
        {
            UserPoolId = this.cognitoSettings.UserPoolId,
            Username = userId.ToString(),
        });

        this.logger.LogInformation("Cognito user {userId} deleted", userId);
    }

    private async Task<Role> GetUserRole(Guid userId)
    {
        var groups = await this.cognitoIdentityProvider.AdminListGroupsForUserAsync(new AdminListGroupsForUserRequest()
        {
            UserPoolId = this.cognitoSettings.UserPoolId,
            Username = userId.ToString(),
        });

        var role = groups.Groups.FirstOrDefault()?.GroupName;

        return Enum.Parse<Role>(role ?? Role.Guest.ToString(), true);
    }

    private async Task AddUserToGroup(Guid userId, Role role)
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

    private async Task RemoveUserFromGroup(Guid userId, Role role)
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