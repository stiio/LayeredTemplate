using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.Common.QueryableExtensions;

internal static class TodoListExtensions
{
    public static IQueryable<TodoList> ForUser(this IQueryable<TodoList> query, Guid userId)
    {
        return query.Where(todoList => todoList.UserId == userId);
    }

    public static IQueryable<TodoListRecordDto> MapTodoListRecordDto(this IQueryable<TodoList> query)
    {
        return query.Select(todoList => new TodoListRecordDto()
        {
            Id = todoList.Id,
            UserId = todoList.UserId,
            Name = todoList.Name,
            Type = todoList.Type,
            CreatedAt = todoList.CreatedAt,
        });
    }

    public static IQueryable<TodoListRecordDto> ApplyFilter(this IQueryable<TodoListRecordDto> query, SearchTodoListFilter? filter)
    {
        if (filter == null)
        {
            return query;
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            query = query.Where(todoList => todoList.Name!.Contains(filter.Search));
        }

        if (filter.Types?.Length > 0)
        {
            query = query.Where(todoList => filter.Types.Contains(todoList.Type));
        }

        return query;
    }
}