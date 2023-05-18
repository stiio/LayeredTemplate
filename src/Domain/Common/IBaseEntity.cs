namespace LayeredTemplate.Domain.Common;

public interface IBaseEntity<TKey>
{
    public TKey Id { get; set; }
}