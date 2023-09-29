﻿using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Domain.Enums;

namespace LayeredTemplate.Application.Contracts.Models.TodoLists;

public class TodoListUpdateRequestBody
{
    /// <summary>
    /// Name of TodoList
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Type of TodoList
    /// </summary>
    [Required]
    public TodoListType Type { get; set; }
}