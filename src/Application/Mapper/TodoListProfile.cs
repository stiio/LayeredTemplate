using AutoMapper;
using LayeredTemplate.Application.Contracts.Models.TodoLists;
using LayeredTemplate.Domain.Entities;

namespace LayeredTemplate.Application.Mapper;

public class TodoListProfile : Profile
{
    public TodoListProfile()
    {
        this.CreateMap<TodoList, TodoListDto>();
        this.CreateMap<TodoList, TodoListRecordDto>();

        this.CreateMap<TodoListUpdateRequestBody, TodoList>();
    }
}