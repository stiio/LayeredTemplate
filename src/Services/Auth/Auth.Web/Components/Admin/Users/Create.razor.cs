using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Components.Account;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Plugins.PhoneHelpers.Attributes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Admin.Users;

public partial class Create : ComponentBase
{
    private string? message;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private ILogger<Create> Logger { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    protected override void OnInitialized()
    {
        this.Input ??= new();
    }

    private async Task OnValidSubmitAsync()
    {
        var existing = await this.UserManager.FindByEmailAsync(this.Input.Email);
        if (existing is not null)
        {
            this.message = $"Error: user with email '{this.Input.Email}' already exists.";
            return;
        }

        var phone = string.IsNullOrWhiteSpace(this.Input.PhoneNumber) ? null : this.Input.PhoneNumber.Trim();

        var user = new ApplicationUser
        {
            UserName = this.Input.Email,
            Email = this.Input.Email,
            EmailConfirmed = this.Input.EmailConfirmed,
            PhoneNumber = phone,
            // Phone can only be confirmed when there actually is a phone.
            PhoneNumberConfirmed = phone is not null && this.Input.PhoneNumberConfirmed,
            FirstName = string.IsNullOrWhiteSpace(this.Input.FirstName) ? null : this.Input.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(this.Input.LastName) ? null : this.Input.LastName.Trim(),
        };

        var result = string.IsNullOrEmpty(this.Input.Password)
            ? await this.UserManager.CreateAsync(user)
            : await this.UserManager.CreateAsync(user, this.Input.Password);

        if (!result.Succeeded)
        {
            this.message = $"Error: {string.Join(" ", result.Errors.Select(e => e.Description))}";
            return;
        }

        this.Logger.LogWarning(
            "Admin {AdminId} created user {UserId} ({Email}).",
            this.UserManager.GetUserId(this.HttpContext.User),
            user.Id,
            user.Email);

        this.RedirectManager.RedirectToWithStatus(
            $"admin/users/details/{user.Id}",
            $"User '{user.Email}' created.",
            this.HttpContext);
    }

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [MaxLength(128)]
        public string Email { get; set; } = string.Empty;

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

        [Phone]
        [MaxLength(20)]
        [NormalizedPhone]
        public string? PhoneNumber
        {
            get;
            set => field = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be 6-100 characters.")]
        public string? Password
        {
            get;
            set => field = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public bool EmailConfirmed { get; set; } = true;

        public bool PhoneNumberConfirmed { get; set; }
    }
}
