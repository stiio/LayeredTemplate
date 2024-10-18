namespace LayeredTemplate.App.Application.Features.Users.Models;

public class CurrentUser
{
    /// <summary>
    /// Id of user
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Email of user
    /// </summary>
    /// <example>example@email.com</example>
    public string? Email { get; set; }

    /// <summary>
    /// Email Verified
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// Phone of user
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Phone Verified
    /// </summary>
    public bool PhoneVerified { get; set; }

    /// <summary>
    /// First Name of user
    /// </summary>
    /// <example>John</example>
    public string? FirstName { get; set; }

    /// <summary>
    /// Last Name of user
    /// </summary>
    /// <example>Doe</example>
    public string? LastName { get; set; }
}