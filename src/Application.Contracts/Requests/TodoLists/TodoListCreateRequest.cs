﻿using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models.TodoLists;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Contracts.Requests.TodoLists;

/// <summary>
/// TodoListCreateRequest
/// </summary>
public class TodoListCreateRequest : IRequest<TodoListDto>
{
    /// <summary>
    /// Body
    /// </summary>
    [FromBody]
    [Required]
    public TodoListCreateRequestBody Body { get; set; } = null!;
}