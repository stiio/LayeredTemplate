using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Web;
using LayeredTemplate.Plugins.Logging.HttpClientLog.Models;
using LayeredTemplate.Plugins.Logging.HttpClientLog.Options;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.Logging.HttpClientLog.Handlers;

/// <summary>
/// A <see cref="DelegatingHandler"/> that logs HTTP request/response details
/// as structured log properties via <see cref="ILogger.BeginScope{TState}"/>.
/// </summary>
internal sealed class LoggingHandler : System.Net.Http.DelegatingHandler
{
    private const string TruncatedMarker = "...[TRUNCATED]";

    private readonly HttpClientLoggingOptions options;
    private readonly ILogger logger;

    public LoggingHandler(HttpClientLoggingOptions options, ILogger logger)
    {
        this.options = options;
        this.logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Buffer the request body before sending because the content stream
        // may be consumed by the inner handler and become unreadable.
        string? requestBody = null;
        if (this.ShouldCaptureRequestBody())
        {
            requestBody = request.Content != null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;
        }

        var stopwatch = Stopwatch.StartNew();
        var response = await base.SendAsync(request, cancellationToken);
        stopwatch.Stop();

        bool isFailure = !response.IsSuccessStatusCode;
        bool shouldLog = this.options.LogMode == LogMode.LogAll
            || (this.options.LogMode == LogMode.LogFailures && isFailure);

        if (!shouldLog)
        {
            return response;
        }

        string requestUri = this.MaskQueryString(request.RequestUri);

        var scopeProperties = new Dictionary<string, object?>
        {
            ["LogType"] = "SendHttpRequest",
            ["StatusCode"] = (int)response.StatusCode,
            ["Elapsed"] = stopwatch.ElapsedMilliseconds,
        };

        string? requestContentType = request.Content?.Headers.ContentType?.MediaType;

        this.CollectRequestHeaders(scopeProperties, request, isFailure);
        this.CollectRequestBody(scopeProperties, requestBody, requestContentType, isFailure);
        this.CollectResponseHeaders(scopeProperties, response, isFailure);
        await this.CollectResponseBodyAsync(scopeProperties, response, isFailure, cancellationToken);

        using (this.logger.BeginScope(scopeProperties))
        {
            if (isFailure)
            {
                this.logger.LogWarning(
                    $"Send http request {request.Method} {requestUri}");
            }
            else
            {
                this.logger.LogInformation(
                    $"Send http request {request.Method} {requestUri}");
            }
        }

        return response;
    }

    private static bool ShouldInclude(LogMode sectionMode, bool isFailure) =>
        sectionMode == LogMode.LogAll || (sectionMode == LogMode.LogFailures && isFailure);

    private void CollectRequestHeaders(
        Dictionary<string, object?> scope,
        HttpRequestMessage request,
        bool isFailure)
    {
        if (!ShouldInclude(this.options.RequestHeadersLogMode, isFailure))
        {
            return;
        }

        bool mask = this.options.MaskedTargets.HasFlag(MaskTarget.RequestHeaders);
        var headers = this.ToDictionary(request.Headers, mask);

        if (request.Content != null)
        {
            foreach (var kvp in this.ToDictionary(request.Content.Headers, mask))
            {
                headers[kvp.Key] = kvp.Value;
            }
        }

        scope["RequestHeaders"] = headers;
    }

    private void CollectRequestBody(
        Dictionary<string, object?> scope,
        string? body,
        string? contentType,
        bool isFailure)
    {
        if (!ShouldInclude(this.options.RequestBodyLogMode, isFailure) || body is null)
        {
            return;
        }

        bool mask = this.options.MaskedTargets.HasFlag(MaskTarget.RequestBody);
        scope["RequestBody"] = this.FormatBody(body, mask, this.options.RequestBodyLogTextLengthLimit, contentType);
    }

    private void CollectResponseHeaders(
        Dictionary<string, object?> scope,
        HttpResponseMessage response,
        bool isFailure)
    {
        if (!ShouldInclude(this.options.ResponseHeadersLogMode, isFailure))
        {
            return;
        }

        bool mask = this.options.MaskedTargets.HasFlag(MaskTarget.ResponseHeaders);
        var headers = this.ToDictionary(response.Headers, mask);

        foreach (var kvp in this.ToDictionary(response.Content.Headers, mask))
        {
            headers[kvp.Key] = kvp.Value;
        }

        scope["ResponseHeaders"] = headers;
    }

    private async Task CollectResponseBodyAsync(
        Dictionary<string, object?> scope,
        HttpResponseMessage response,
        bool isFailure,
        CancellationToken cancellationToken)
    {
        if (!ShouldInclude(this.options.ResponseBodyLogMode, isFailure))
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? contentType = response.Content.Headers.ContentType?.MediaType;

        bool mask = this.options.MaskedTargets.HasFlag(MaskTarget.ResponseBody);
        scope["ResponseBody"] = this.FormatBody(body, mask, this.options.ResponseBodyLogTextLengthLimit, contentType);
    }

    private bool ShouldCaptureRequestBody()
    {
        // None means "never log body" — skip the read entirely.
        if (this.options.RequestBodyLogMode == LogMode.None)
        {
            return false;
        }

        // We must capture the body eagerly (before SendAsync) if there is
        // any chance we will need it later. The body stream is consumed
        // by the inner handler, so we cannot read it afterwards.
        return this.options.RequestBodyLogMode == LogMode.LogAll
            || this.options.LogMode != LogMode.None;
    }

    private string MaskQueryString(Uri? uri)
    {
        if (uri == null)
        {
            return string.Empty;
        }

        if (!this.options.MaskedTargets.HasFlag(MaskTarget.QueryString)
            || this.options.MaskedProperties.Count == 0
            || string.IsNullOrEmpty(uri.Query))
        {
            return uri.ToString();
        }

        var parsed = HttpUtility.ParseQueryString(uri.Query);
        foreach (string? key in parsed.AllKeys)
        {
            if (key != null && this.options.MaskedProperties.Contains(key))
            {
                parsed[key] = this.options.MaskFormat;
            }
        }

        var builder = new UriBuilder(uri) { Query = parsed.ToString() };
        return builder.Uri.ToString();
    }

    private Dictionary<string, string> ToDictionary(HttpHeaders headers, bool mask)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            string value = string.Join(", ", header.Value);

            if (mask && this.options.MaskedProperties.Contains(header.Key))
            {
                value = this.options.MaskFormat;
            }

            result[header.Key] = value;
        }

        return result;
    }

    private string FormatBody(string body, bool mask, int? lengthLimit, string? contentType)
    {
        string result = mask ? this.MaskBody(body, contentType) : body;

        if (lengthLimit.HasValue && result.Length > lengthLimit.Value)
        {
            return string.Concat(result.AsSpan(0, lengthLimit.Value), TruncatedMarker);
        }

        return result;
    }

    /// <summary>
    /// Dispatches body masking to the appropriate strategy based on content type.
    /// </summary>
    private string MaskBody(string body, string? contentType)
    {
        if (this.options.MaskedProperties.Count == 0)
        {
            return body;
        }

        return contentType switch
        {
            "application/x-www-form-urlencoded" => this.MaskFormUrlEncodedBody(body),
            "multipart/form-data" => this.MaskMultipartFormDataBody(body),
            _ => this.MaskJsonProperties(body),
        };
    }

    /// <summary>
    /// Replaces values of JSON properties whose names appear in
    /// <see cref="HttpClientLoggingOptions.MaskedProperties"/> with the configured mask string.
    /// Handles string, numeric, boolean and null JSON values at any nesting depth.
    /// </summary>
    private string MaskJsonProperties(string body)
    {
        foreach (string property in this.options.MaskedProperties)
        {
            string escaped = Regex.Escape(property);
            var pattern = @$"(""{escaped}"")(\s*:\s*)(?:""[^""\\]*(?:\\.[^""\\]*)*""|\d+(?:\.\d+)?|true|false|null)";

            string replacement = $@"$1$2""{this.options.MaskFormat}""";
            body = Regex.Replace(body, pattern, replacement, RegexOptions.IgnoreCase);
        }

        return body;
    }

    /// <summary>
    /// Masks values of matched keys in a <c>application/x-www-form-urlencoded</c> body
    /// (e.g. <c>user=admin&amp;password=secret</c> → <c>user=admin&amp;password=***</c>).
    /// </summary>
    private string MaskFormUrlEncodedBody(string body)
    {
        var parsed = HttpUtility.ParseQueryString(body);

        foreach (string? key in parsed.AllKeys)
        {
            if (key != null && this.options.MaskedProperties.Contains(key))
            {
                parsed[key] = this.options.MaskFormat;
            }
        }

        return parsed.ToString() ?? body;
    }

    /// <summary>
    /// Masks field values in a <c>multipart/form-data</c> body for fields whose
    /// <c>name</c> appears in <see cref="HttpClientLoggingOptions.MaskedProperties"/>.
    /// File parts (<c>filename=</c>) are left untouched — only simple form values are masked.
    /// </summary>
    /// <remarks>
    /// Handles both quoted and unquoted name values:
    /// <c>name="Token"</c> and <c>name=Token</c>.
    /// Handles additional part headers (e.g. <c>Content-Type</c>) between
    /// <c>Content-Disposition</c> and the blank-line separator.
    /// </remarks>
    private string MaskMultipartFormDataBody(string body)
    {
        // A multipart part may look like either of:
        //
        //   Content-Disposition: form-data; name=Token\r\n        ← no quotes
        //   \r\n
        //   some token\r\n
        //
        //   Content-Type: text/plain; charset=utf-8\r\n           ← extra header before disposition
        //   Content-Disposition: form-data; name="Token"\r\n      ← with quotes
        //   \r\n
        //   some token\r\n
        //
        // The regex:
        //   1. Finds Content-Disposition line with name=<property> (quotes optional).
        //   2. Negative lookahead rejects file parts (filename=).
        //   3. Consumes any remaining header lines until the blank-line separator.
        //   4. Captures the value line that follows the blank line.
        foreach (string property in this.options.MaskedProperties)
        {
            string escaped = Regex.Escape(property);

            var pattern =
                @$"(Content-Disposition:\s*form-data;[^\r\n]*\bname=""?{escaped}""?" +
                @"(?=[""\s;\r\n])" +
                @"(?![^\r\n]*\bfilename\s*=)" +
                @"[^\r\n]*\r?\n" +
                @"(?:[^\r\n]+\r?\n)*?" +
                @"\r?\n)" +
                @"([^\r\n]+)";

            string replacement = $"$1{this.options.MaskFormat}";
            body = Regex.Replace(body, pattern, replacement, RegexOptions.IgnoreCase);
        }

        return body;
    }
}
