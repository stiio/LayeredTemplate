using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Application.Common.Models;

namespace LayeredTemplate.Application.TodoLists.Models;

public class TodoListSearchResponse
{
    [Required]
    public PaginationResponse Pagination { get; set; } = null!;

    [Required]
    public Sorting<TodoListRecordDto> Sorting { get; set; } = null!;

    public TodoListRecordDtoFilter? Filter { get; set; }

    [Required]
    public TodoListRecordDto[] Data { get; set; } = null!;
}