using LayeredTemplate.Plugins.Logging.HttpClientLog.Handlers;
using LayeredTemplate.Plugins.Logging.HttpClientLog.Models;

namespace LayeredTemplate.Plugins.Logging.HttpClientLog.Options;

/// <summary>
/// Per-HttpClient logging configuration for <see cref="LoggingHandler"/>.
/// Each named/typed HttpClient can have its own instance of these options.
/// </summary>
public sealed class HttpClientLoggingOptions
{
    // ── Global gate ───────────────────────────────────────────────

    /// <summary>
    /// Master switch: determines whether the handler produces a log entry at all.
    /// <see cref="LogMode.None"/> disables logging entirely;
    /// <see cref="LogMode.LogAll"/> logs every exchange;
    /// <see cref="LogMode.LogFailures"/> only logs non-2XX responses.
    /// </summary>
    public LogMode LogMode { get; set; } = LogMode.LogAll;

    // ── Per-section gates ─────────────────────────────────────────

    /// <summary>When to include request headers in the log entry.</summary>
    public LogMode RequestHeadersLogMode { get; set; } = LogMode.LogFailures;

    /// <summary>When to include request body in the log entry.</summary>
    public LogMode RequestBodyLogMode { get; set; } = LogMode.LogFailures;

    /// <summary>When to include response headers in the log entry.</summary>
    public LogMode ResponseHeadersLogMode { get; set; } = LogMode.LogFailures;

    /// <summary>When to include response body in the log entry.</summary>
    public LogMode ResponseBodyLogMode { get; set; } = LogMode.LogFailures;

    // ── Body size limits ──────────────────────────────────────────

    /// <summary>
    /// Maximum number of characters to capture from the request body.
    /// <c>null</c> means no limit.
    /// When the body is truncated, a marker is appended to the logged text.
    /// </summary>
    public int? RequestBodyLogTextLengthLimit { get; set; }

    /// <summary>
    /// Maximum number of characters to capture from the response body.
    /// <c>null</c> means no limit.
    /// When the body is truncated, a marker is appended to the logged text.
    /// </summary>
    public int? ResponseBodyLogTextLengthLimit { get; set; }

    // ── Sensitive data masking ────────────────────────────────────

    /// <summary>
    /// The replacement string used when masking sensitive values.
    /// Defaults to <c>"***"</c>.
    /// </summary>
    public string MaskFormat { get; set; } = "***";

    /// <summary>
    /// Property / header names whose values should be masked (case-insensitive).
    /// For headers: compared against header name.
    /// For JSON bodies: compared against JSON property names at any nesting depth.
    /// </summary>
    public HashSet<string> MaskedProperties { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Flags that select which parts of the HTTP exchange are subject to masking.
    /// </summary>
    public MaskTarget MaskedTargets { get; set; } = MaskTarget.None;
}
