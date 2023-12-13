using AutoMapper;
using LayeredTemplate.Application.TodoLists.Models;
using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.TodoLists.Mapper;

public class TodoListProfile : Profile
{
    public TodoListProfile()
    {
        this.CreateMap<TodoList, TodoListDto>();
        this.CreateMap<TodoList, TodoListRecordDto>();

        this.CreateMap<TodoListUpdateRequestBody, TodoList>();
    }
}