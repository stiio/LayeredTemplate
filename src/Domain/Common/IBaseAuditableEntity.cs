namespace LayeredTemplate.Domain.Common;

public interface IBaseAuditableEntity<TKey> : IBaseEntity<TKey>, ITimeStamp
{
}