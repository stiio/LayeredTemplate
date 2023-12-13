using AutoMapper;
using LayeredTemplate.Application.Features.TodoLists.Models;
using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.Features.TodoLists.Mapper;

public class TodoListProfile : Profile
{
    public TodoListProfile()
    {
        this.CreateMap<TodoList, TodoListDto>();
        this.CreateMap<TodoList, TodoListRecordDto>();

        this.CreateMap<TodoListUpdateRequestBody, TodoList>();
    }
}