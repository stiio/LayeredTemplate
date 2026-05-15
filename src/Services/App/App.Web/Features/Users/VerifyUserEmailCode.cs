using System.ComponentModel.DataAnnotations;
using LayeredTemplate.App.Shared.Validation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Features.Users;

public static class VerifyUserEmailCode
{
    public sealed class Request
    {
        /// <example>example@email.com</example>
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = null!;

        /// <example>124567</example>
        [Required]
        public int Code { get; set; }
    }

    public static void Configure(RouteGroupBuilder group) =>
        group.MapPut("/email/verify_code", Handle)
            .WithName(nameof(VerifyUserEmailCode))
            .WithSummary("Verify user email confirmation code")
            .WithValidation<Request>();

    public static Task<NoContent> Handle([FromBody] Request request) =>
        Task.FromResult(TypedResults.NoContent());
}
