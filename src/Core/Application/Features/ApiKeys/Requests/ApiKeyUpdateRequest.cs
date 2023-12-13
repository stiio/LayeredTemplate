using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Features.ApiKeys.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Features.ApiKeys.Requests;

public class ApiKeyUpdateRequest : IRequest<ApiKeyDto>
{
    public Guid Id { get; set; }

    [Required]
    [FromBody]
    public ApiKeyUpdateRequestBody Body { get; set; } = null!;
}