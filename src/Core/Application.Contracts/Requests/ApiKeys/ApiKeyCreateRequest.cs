using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models.ApiKeys;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Contracts.Requests.ApiKeys;

public class ApiKeyCreateRequest : IRequest<ApiKeySecretDto>
{
    [Required]
    [FromBody]
    public ApiKeyCreateRequestBody Body { get; set; } = null!;
}