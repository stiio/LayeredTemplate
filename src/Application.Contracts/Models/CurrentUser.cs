namespace LayeredTemplate.Application.Contracts.Models;

/// <summary>
/// Current User
/// </summary>
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
    /// Name of user
    /// </summary>
    /// <example>John Doe</example>
    public string? Name { get; set; }
}