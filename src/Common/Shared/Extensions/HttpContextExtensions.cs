using Microsoft.AspNetCore.Http;

namespace LayeredTemplate.Shared.Extensions;

public static class HttpContextExtensions
{
    public static string? GetRequestIp(this HttpContext context, bool tryUseXForwardHeader = true)
    {
        string? ip = null;

        // X-Forwarded-For (csv list):  Using the First entry in the list seems to work
        // for 99% of cases however it has been suggested that a better (although tedious)
        // approach might be to read each IP from right to left and use the first public IP.
        // http://stackoverflow.com/a/43554000/538763
        if (tryUseXForwardHeader)
        {
            ip = context.Request.GetHeaderValueAs<string>("X-Forwarded-For")?.SplitCsv()?.FirstOrDefault();
        }

        // RemoteIpAddress is always null in DNX RC1 Update1 (bug).
        if (string.IsNullOrWhiteSpace(ip) && context.Connection.RemoteIpAddress != null)
        {
            ip = context.Connection.RemoteIpAddress.ToString();
        }

        if (string.IsNullOrWhiteSpace(ip))
        {
            ip = context.Request.GetHeaderValueAs<string>("REMOTE_ADDR");
        }

        return ip;
    }

    private static T? GetHeaderValueAs<T>(this HttpRequest request, string headerName)
    {
        if (request?.Headers?.TryGetValue(headerName, out var values) ?? false)
        {
            string rawValues = values.ToString();   // writes out as Csv when there are multiple.

            if (!string.IsNullOrWhiteSpace(rawValues))
            {
                return (T)Convert.ChangeType(values.ToString(), typeof(T));
            }
        }

        return default;
    }

    private static List<string>? SplitCsv(this string csvList, bool nullOrWhitespaceInputReturnsNull = false)
    {
        if (string.IsNullOrWhiteSpace(csvList))
        {
            return nullOrWhitespaceInputReturnsNull ? null : new List<string>();
        }

        return csvList
            .TrimEnd(',')
            .Split(',')
            .AsEnumerable<string>()
            .Select(s => s.Trim())
            .ToList();
    }
}