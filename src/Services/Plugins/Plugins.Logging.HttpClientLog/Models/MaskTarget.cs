namespace LayeredTemplate.Plugins.Logging.HttpClientLog.Models;

/// <summary>
/// Flags that indicate which parts of the HTTP exchange are subject to sensitive data masking.
/// </summary>
[Flags]
public enum MaskTarget
{
    None = 0,
    RequestHeaders = 1 << 0,
    ResponseHeaders = 1 << 1,
    RequestBody = 1 << 2,
    ResponseBody = 1 << 3,
    QueryString = 1 << 4,
}
