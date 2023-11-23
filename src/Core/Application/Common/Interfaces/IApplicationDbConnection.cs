namespace LayeredTemplate.Application.Common.Interfaces;

public interface IApplicationDbConnection
{
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null);

    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null);

    Task<T> QueryFirstAsync<T>(string sql, object? param = null);

    Task<T> QuerySingleAsync<T>(string sql, object? param = null);

    Task<int> ExecuteAsync(string sql, object? param = null);

    Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null);
}