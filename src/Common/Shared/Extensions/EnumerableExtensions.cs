namespace LayeredTemplate.Shared.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T[]> Paged<T>(this IEnumerable<T> source, int limit)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var array = source.ToArray();

        var pageNumber = 1;
        var page = array.Take(limit).ToArray();

        while (page.Any())
        {
            yield return page;

            pageNumber++;
            page = array.Skip((pageNumber - 1) * limit).Take(limit).ToArray();
        }
    }
}