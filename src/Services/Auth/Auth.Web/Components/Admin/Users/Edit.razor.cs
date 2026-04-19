using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Components.Account;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Plugins.PhoneHelpers.Attributes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Admin.Users;

public partial class Edit : ComponentBase
{
    private string? message;
    private bool notFound;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private ILogger<Edit> Logger { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [Parameter]
    public string Id { get; set; } = string.Empty;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        // Set Input before the first await so EditForm has a non-null Model on initial render.
        this.Input ??= new();

        var user = await this.UserManager.FindByIdAsync(this.Id);
        if (user is null)
        {
            this.notFound = true;
            return;
        }

        // Pre-fill only on GET. On POST, SupplyParameterFromForm has already populated Input
        // with submitted values — overwriting them from the DB would swallow the user's edits.
        if (HttpMethods.IsGet(this.HttpContext.Request.Method))
        {
            this.Input.Email = user.Email ?? string.Empty;
            this.Input.EmailConfirmed = user.EmailConfirmed;
            this.Input.PhoneNumber = user.PhoneNumber;
            this.Input.PhoneNumberConfirmed = user.PhoneNumberConfirmed;
            this.Input.FirstName = user.FirstName;
            this.Input.LastName = user.LastName;
        }
    }

    private async Task OnValidSubmitAsync()
    {
        var user = await this.UserManager.FindByIdAsync(this.Id);
        if (user is null)
        {
            this.notFound = true;
            return;
        }

        var newEmail = this.Input.Email.Trim();
        var newPhone = string.IsNullOrWhiteSpace(this.Input.PhoneNumber) ? null : this.Input.PhoneNumber.Trim();

        // Email change uses SetEmailAsync (resets EmailConfirmed and normalizes). Keep UserName in sync.
        if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await this.UserManager.FindByEmailAsync(newEmail);
            if (clash is not null && clash.Id != user.Id)
            {
                this.message = $"Error: another user already has email '{newEmail}'.";
                return;
            }

            var setEmail = await this.UserManager.SetEmailAsync(user, newEmail);
            if (!setEmail.Succeeded)
            {
                this.message = $"Error: {string.Join(" ", setEmail.Errors.Select(e => e.Description))}";
                return;
            }

            var setUserName = await this.UserManager.SetUserNameAsync(user, newEmail);
            if (!setUserName.Succeeded)
            {
                this.message = $"Error: {string.Join(" ", setUserName.Errors.Select(e => e.Description))}";
                return;
            }
        }

        // Phone change resets PhoneNumberConfirmed; only call when actually changing.
        if (!string.Equals(user.PhoneNumber, newPhone, StringComparison.Ordinal))
        {
            var setPhone = await this.UserManager.SetPhoneNumberAsync(user, newPhone);
            if (!setPhone.Succeeded)
            {
                this.message = $"Error: {string.Join(" ", setPhone.Errors.Select(e => e.Description))}";
                return;
            }
        }

        // Apply confirmation flags last — SetEmailAsync/SetPhoneNumberAsync force them back to false.
        user.EmailConfirmed = this.Input.EmailConfirmed;
        user.PhoneNumberConfirmed = user.PhoneNumber is not null && this.Input.PhoneNumberConfirmed;

        user.FirstName = string.IsNullOrWhiteSpace(this.Input.FirstName) ? null : this.Input.FirstName.Trim();
        user.LastName = string.IsNullOrWhiteSpace(this.Input.LastName) ? null : this.Input.LastName.Trim();

        var result = await this.UserManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            this.message = $"Error: {string.Join(" ", result.Errors.Select(e => e.Description))}";
            return;
        }

        this.Logger.LogInformation(
            "Admin {AdminId} updated profile for user {UserId}.",
            this.UserManager.GetUserId(this.HttpContext.User),
            this.Id);

        this.RedirectManager.RedirectToWithStatus(
            $"admin/users/details/{this.Id}",
            "User profile saved.",
            this.HttpContext);
    }

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [MaxLength(128)]
        public string Email { get; set; } = string.Empty;

        public bool EmailConfirmed { get; set; }

        [NormalizedPhone]
        [MaxLength(20)]
        public string? PhoneNumber
        {
            get;
            set => field = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public bool PhoneNumberConfirmed { get; set; }

        [MaxLength(100)]
        public string? FirstName
        {
            get;
            set => field = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        [MaxLength(100)]
        public string? LastName
        {
            get;
            set => field = string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
