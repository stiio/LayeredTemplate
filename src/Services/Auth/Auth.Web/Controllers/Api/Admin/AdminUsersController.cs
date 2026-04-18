using System.ComponentModel.DataAnnotations;
using System.Text;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Validation.AspNetCore;

namespace LayeredTemplate.Auth.Web.Controllers.Api.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Policy = AppAuthorizationPolicies.ScopeAdminUsers)]
public class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly ILogger<AdminUsersController> logger;

    public AdminUsersController(UserManager<ApplicationUser> userManager, ILogger<AdminUsersController> logger)
    {
        this.userManager = userManager;
        this.logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponse>> GetById(string id, CancellationToken cancellationToken)
    {
        var user = await this.userManager.FindByIdAsync(id);
        if (user is null)
        {
            return this.NotFound();
        }

        return await this.ToResponseAsync(user);
    }

    [HttpGet]
    public async Task<ActionResult<UserResponse>> GetByEmail([FromQuery, Required] string email, CancellationToken cancellationToken)
    {
        // [Required] + [ApiController] already short-circuits empty/missing email into a 400 ValidationProblemDetails;
        // this extra guard only catches whitespace-only strings which pass [Required].
        if (string.IsNullOrWhiteSpace(email))
        {
            this.ModelState.AddModelError(nameof(email), "Query parameter 'email' is required.");
            return this.ValidationProblem();
        }

        var user = await this.userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return this.NotFound();
        }

        return this.Ok(await this.ToResponseAsync(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var existing = await this.userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return this.Problem(
                title: "Conflict",
                detail: $"A user with email '{request.Email}' already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = request.EmailConfirmed,
            PhoneNumber = request.PhoneNumber,
            PhoneNumberConfirmed = request.PhoneNumberConfirmed,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        var createResult = string.IsNullOrEmpty(request.Password)
            ? await this.userManager.CreateAsync(user)
            : await this.userManager.CreateAsync(user, request.Password);

        if (!createResult.Succeeded)
        {
            return this.IdentityValidationProblem(createResult, passwordField: nameof(CreateUserRequest.Password));
        }

        this.logger.LogInformation("Admin API created user {UserId} ({Email}) from client {ClientId}.",
            user.Id, user.Email, this.HttpContext.User.Identity?.Name);

        return this.CreatedAtAction(nameof(this.GetById), new { id = user.Id }, await this.ToResponseAsync(user));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<UserResponse>> Update(string id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await this.userManager.FindByIdAsync(id);
        if (user is null)
        {
            return this.NotFound();
        }

        if (request.EmailConfirmed == false)
        {
            this.ModelState.AddModelError(
                nameof(UpdateUserRequest.EmailConfirmed),
                "Setting EmailConfirmed to false via API is not supported.");
            return this.ValidationProblem();
        }

        var clientId = this.HttpContext.User.Identity?.Name;

        user.EmailConfirmed = request.EmailConfirmed ?? user.EmailConfirmed;
        user.PhoneNumber = request.PhoneNumber ?? user.PhoneNumber;
        user.PhoneNumberConfirmed = request.PhoneNumberConfirmed ?? user.PhoneNumberConfirmed;
        user.FirstName = request.FirstName ?? user.FirstName;
        user.LastName = request.LastName ?? user.LastName;

        var updateUserResult = await this.userManager.UpdateAsync(user);
        if (!updateUserResult.Succeeded)
        {
            return this.IdentityValidationProblem(updateUserResult);
        }

        this.logger.LogInformation("Admin API updated user {UserId} from client {ClientId}.", id, clientId);

        if (request.NewPassword is not null)
        {
            IdentityResult pwdResult;
            if (await this.userManager.HasPasswordAsync(user))
            {
                var token = await this.userManager.GeneratePasswordResetTokenAsync(user);
                pwdResult = await this.userManager.ResetPasswordAsync(user, token, request.NewPassword);
            }
            else
            {
                pwdResult = await this.userManager.AddPasswordAsync(user, request.NewPassword);
            }

            if (!pwdResult.Succeeded)
            {
                return this.IdentityValidationProblem(pwdResult, passwordField: nameof(UpdateUserRequest.NewPassword));
            }

            this.logger.LogWarning("Admin API set password for user {UserId} from client {ClientId}.", id, clientId);
        }

        return this.Ok(await this.ToResponseAsync(user));
    }

    [HttpPost("{id}/invite-token")]
    public async Task<ActionResult<InviteTokenResponse>> CreateInviteToken(string id, CancellationToken cancellationToken)
    {
        var user = await this.userManager.FindByIdAsync(id);
        if (user is null)
        {
            return this.NotFound();
        }

        var token = await this.userManager.GenerateUserTokenAsync(
            user,
            InviteTokenSettings.ProviderName,
            InviteTokenSettings.Purpose);

        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        this.logger.LogInformation(
            "Admin API issued invite token for user {UserId} from client {ClientId}.",
            id,
            this.HttpContext.User.Identity?.Name);

        return this.Ok(new InviteTokenResponse(
            UserId: user.Id,
            Token: encoded,
            ExpiresAt: DateTimeOffset.UtcNow + InviteTokenSettings.Lifespan));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var user = await this.userManager.FindByIdAsync(id);
        if (user is null)
        {
            return this.NotFound();
        }

        var result = await this.userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return this.IdentityValidationProblem(result);
        }

        this.logger.LogWarning("Admin API deleted user {UserId} from client {ClientId}.",
            id, this.HttpContext.User.Identity?.Name);

        return this.NoContent();
    }

    private async Task<UserResponse> ToResponseAsync(ApplicationUser user) =>
        new(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            EmailConfirmed: user.EmailConfirmed,
            PhoneNumber: user.PhoneNumber,
            PhoneNumberConfirmed: user.PhoneNumberConfirmed,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Roles: (await this.userManager.GetRolesAsync(user)).ToArray(),
            HasPassword: await this.userManager.HasPasswordAsync(user));

    /// <summary>
    /// Maps an <see cref="IdentityResult"/> failure to an RFC 7807 <see cref="ValidationProblemDetails"/> (HTTP 400).
    /// Identity error codes are routed to the request fields users understand (<c>Email</c>, <c>Password</c>/<paramref name="passwordField"/>);
    /// unrecognised codes are surfaced as top-level errors under the empty key.
    /// </summary>
    private ActionResult IdentityValidationProblem(IdentityResult result, string passwordField = nameof(CreateUserRequest.Password))
    {
        foreach (var error in result.Errors)
        {
            var field = error.Code switch
            {
                "DuplicateEmail" or "InvalidEmail" => nameof(CreateUserRequest.Email),
                "DuplicateUserName" or "InvalidUserName" => nameof(CreateUserRequest.Email),
                { } code when code.StartsWith("Password", StringComparison.Ordinal) => passwordField,
                _ => string.Empty,
            };
            this.ModelState.AddModelError(field, error.Description);
        }

        return this.ValidationProblem();
    }
}
