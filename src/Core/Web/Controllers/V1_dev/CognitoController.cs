using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using LayeredTemplate.Domain.Enums;
using LayeredTemplate.Shared.Options;
using LayeredTemplate.Web.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Web.Controllers.V1_dev;

[ApiController]
[Route("cognito")]
public class CognitoController : AppControllerBase
{
    private readonly IAmazonCognitoIdentityProvider cognitoIdentityProvider;
    private readonly CognitoSettings cognitoSettings;

    public CognitoController(
        IAmazonCognitoIdentityProvider cognitoIdentityProvider,
        IOptions<CognitoSettings> cognitoSettings)
    {
        this.cognitoIdentityProvider = cognitoIdentityProvider;
        this.cognitoSettings = cognitoSettings.Value;
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(string email, Role role)
    {
        var adminCreateUserRequest = new AdminCreateUserRequest()
        {
            Username = email,
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
                    Value = email,
                },
                new AttributeType()
                {
                    Name = "email_verified",
                    Value = "true",
                },
            },
            MessageAction = MessageActionType.SUPPRESS,
        };

        var response = await this.cognitoIdentityProvider.AdminCreateUserAsync(adminCreateUserRequest);

        await this.cognitoIdentityProvider.AdminAddUserToGroupAsync(
            new AdminAddUserToGroupRequest()
            {
                UserPoolId = this.cognitoSettings.UserPoolId,
                Username = response.User.Username,
                GroupName = role.ToString(),
            });

        return this.Ok(response);
    }

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(string? nextToken)
    {
        var response = await this.cognitoIdentityProvider.ListUsersAsync(new ListUsersRequest()
        {
            UserPoolId = this.cognitoSettings.UserPoolId,
            PaginationToken = nextToken,
        });

        return this.Ok(response);
    }

    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUser(string userId)
    {
        var response = await this.cognitoIdentityProvider.AdminGetUserAsync(new AdminGetUserRequest()
        {
            UserPoolId = this.cognitoSettings.UserPoolId,
            Username = userId,
        });

        return this.Ok(response);
    }

    [HttpPost("users/{userId}/password")]
    public async Task<IActionResult> SetUserPassword(string userId, string password)
    {
        var response = await this.cognitoIdentityProvider.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest()
        {
            UserPoolId = this.cognitoSettings.UserPoolId,
            Username = userId.ToString(),
            Password = password,
            Permanent = true,
        });

        return this.Ok(response);
    }

    [HttpGet("users/{userId}/group")]
    public async Task<IActionResult> ListUserGroups(string userId)
    {
        var response = await this.cognitoIdentityProvider.AdminListGroupsForUserAsync(new AdminListGroupsForUserRequest()
        {
            UserPoolId = this.cognitoSettings.UserPoolId,
            Username = userId,
        });

        return this.Ok(response);
    }

    [HttpPost("users/{userId}/group")]
    public async Task<IActionResult> AddUserToGroup(string userId, Role role)
    {
        var response = await this.cognitoIdentityProvider.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest()
        {
            UserPoolId = this.cognitoSettings.UserPoolId,
            Username = userId,
            GroupName = role.ToString(),
        });

        return this.Ok(response);
    }

    [HttpDelete("users/{userId}/group")]
    public async Task<IActionResult> RemoveUserFromGroup(string userId, Role role)
    {
        var response = await this.cognitoIdentityProvider.AdminRemoveUserFromGroupAsync(new AdminRemoveUserFromGroupRequest()
        {
            UserPoolId = this.cognitoSettings.UserPoolId,
            Username = userId,
            GroupName = role.ToString(),
        });

        return this.Ok(response);
    }

    [HttpGet("groups")]
    public async Task<IActionResult> ListGroups()
    {
        var response = await this.cognitoIdentityProvider
            .ListGroupsAsync(
                new ListGroupsRequest()
                {
                    UserPoolId = this.cognitoSettings.UserPoolId,
                });

        return this.Ok(response);
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroups()
    {
        foreach (var role in Enum.GetValues<Role>())
        {
            try
            {
                await this.cognitoIdentityProvider
                    .CreateGroupAsync(
                        new CreateGroupRequest()
                        {
                            UserPoolId = this.cognitoSettings.UserPoolId,
                            GroupName = role.ToString(),
                        });
            }
            catch (GroupExistsException)
            {
            }
        }

        return this.Ok();
    }
}