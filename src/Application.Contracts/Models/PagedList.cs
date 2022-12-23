namespace LayeredTemplate.Application.Contracts.Models;

/// <summary>
/// Paged List
/// </summary>
/// <typeparam name="T">Items type.</typeparam>
public class PagedList<T>
{
    /// <summary>
    /// Pagination
    /// </summary>
    public Pagination Pagination { get; set; } = null!;

    /// <summary>
    /// Founded items
    /// </summary>
    public T[] Data { get; set; } = null!;
}