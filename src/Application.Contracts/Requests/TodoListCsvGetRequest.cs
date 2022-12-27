namespace LayeredTemplate.Application.Contracts.Requests;

/// <summary>
/// TodoList Csv Get Request
/// </summary>
public class TodoListCsvGetRequest
{
    /// <summary>
    /// Id of todoList
    /// </summary>
    public Guid Id { get; set; }
}