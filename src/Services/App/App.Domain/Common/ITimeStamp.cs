namespace LayeredTemplate.App.Domain.Common;

public interface ITimeStamp
{
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}