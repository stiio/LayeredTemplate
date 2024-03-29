﻿using LayeredTemplate.Application.Features.Users.Models;

namespace LayeredTemplate.Application.Features.ApiKeys.Models;

public class ApiKeyDto
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public UserShortInfo? User { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}