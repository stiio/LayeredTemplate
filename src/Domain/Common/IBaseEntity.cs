namespace LayeredTemplate.Domain.Common;

public interface IBaseEntity<TKey> : ITimeStamp
{
    public TKey Id { get; set; }
}