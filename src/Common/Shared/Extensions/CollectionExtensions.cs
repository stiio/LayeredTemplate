namespace LayeredTemplate.Shared.Extensions;

public static class CollectionExtensions
{
    public static void AddIfNotNull<TElement>(this ICollection<TElement> target, TElement? element)
    {
        if (element != null)
        {
            target.Add(element);
        }
    }
}