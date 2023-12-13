using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.ApiKeys.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.ApiKeys.Requests;

public class ApiKeyUpdateRequest : IRequest<ApiKeyDto>
{
    public Guid Id { get; set; }

    [Required]
    [FromBody]
    public ApiKeyUpdateRequestBody Body { get; set; } = null!;
}