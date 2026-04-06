namespace LayeredTemplate.Plugins.Logging.HttpClientLog.Models;

/// <summary>
/// Controls when a particular section of the HTTP exchange is logged.
/// </summary>
public enum LogMode
{
    /// <summary>
    /// Never log this section. The data is not captured at all,
    /// avoiding unnecessary reads of request/response streams.
    /// </summary>
    None,

    /// <summary>
    /// Log for every request regardless of the response status code.
    /// </summary>
    LogAll,

    /// <summary>
    /// Log only when the response status code is outside the 2XX range.
    /// </summary>
    LogFailures,
}
