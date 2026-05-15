using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Shared.Validation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.Users;

public static class SendUserEmailCode
{
    public sealed class Request
    {
        /// <summary>Email</summary>
        /// <example>example@email.com</example>
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = null!;
    }

    public static void Configure(RouteGroupBuilder group) =>
        group.MapPost("/email/send_code", Handle)
            .WithName(nameof(SendUserEmailCode))
            .WithSummary("Send user email verification code")
            .WithValidation<Request>();

    public static Task<NoContent> Handle([FromBody] Request request) =>
        Task.FromResult(TypedResults.NoContent());
}
