namespace LayeredTemplate.Auth.ApiClient.Models;

public record UserResponse(
    string Id,
    string Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    string? FirstName,
    string? LastName,
    bool HasPassword,
    string[] Roles);
