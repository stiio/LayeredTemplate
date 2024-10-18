namespace LayeredTemplate.App.Application.Common.Services;

public interface IApplicationDbConnection
{
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);

    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);

    Task<T> QueryFirstAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);

    Task<T> QuerySingleAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);

    Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default);

    Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);
}