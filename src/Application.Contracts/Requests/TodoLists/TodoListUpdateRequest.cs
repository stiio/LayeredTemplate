﻿using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Contracts.Models.TodoLists;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Application.Contracts.Requests.TodoLists;

public class TodoListUpdateRequest : IRequest<TodoListDto>
{
    /// <summary>
    /// Id of TodoList
    /// </summary>
    public Guid Id { get; set; }

    [Required]
    [FromBody]
    public TodoListUpdateRequestBody Body { get; set; } = null!;
}