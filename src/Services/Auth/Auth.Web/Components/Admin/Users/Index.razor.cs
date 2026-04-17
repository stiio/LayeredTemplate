using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Auth.Web.Components.Admin.Users;

public partial class Index : ComponentBase
{
    private const int MaxResults = 50;

    private List<UserRow>? results;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [SupplyParameterFromForm]
    private SearchInput Input { get; set; } = default!;

    protected override void OnInitialized()
    {
        this.Input ??= new();
    }

    private async Task OnSearchAsync()
    {
        var query = this.Input.Email?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            this.results = [];
            return;
        }

        var normalized = this.UserManager.NormalizeEmail(query);

        var rows = await this.UserManager.Users
            .Where(u => u.NormalizedEmail != null && u.NormalizedEmail.Contains(normalized))
            .OrderBy(u => u.NormalizedEmail)
            .Take(MaxResults)
            .Select(u => new UserRow
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                EmailConfirmed = u.EmailConfirmed,
                PhoneNumber = u.PhoneNumber,
                PhoneNumberConfirmed = u.PhoneNumberConfirmed,
                TwoFactorEnabled = u.TwoFactorEnabled,
            })
            .ToListAsync();

        this.results = rows;
    }

    private sealed class SearchInput
    {
        [Required]
        [MaxLength(256)]
        public string? Email { get; set; }
    }

    private sealed class UserRow
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public string? PhoneNumber { get; set; }
        public bool PhoneNumberConfirmed { get; set; }
        public bool TwoFactorEnabled { get; set; }
    }
}
