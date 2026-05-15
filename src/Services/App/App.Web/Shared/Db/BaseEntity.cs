namespace LayeredTemplate.App.Shared.Db;

/// <summary>
/// Common metadata every persisted entity carries. <c>CreatedAt</c> and <c>UpdatedAt</c> are
/// auto-stamped by <see cref="Interceptors.BaseEntitySaveChangesInterceptor"/>, so feature code
/// never sets them directly.
/// </summary>
public interface IBaseEntity
{
    Guid Id { get; set; }
}

public interface ITimeStamp
{
    DateTime CreatedAt { get; set; }

    DateTime UpdatedAt { get; set; }
}

public interface IBaseAuditableEntity : IBaseEntity, ITimeStamp;
