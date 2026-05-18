using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.JsonMultipart;

/// <summary>
/// Reads a single JSON-encoded multipart part out of an incoming request and deserialises it
/// into <typeparamref name="T"/>. Used by <see cref="IJsonMultipartPart{TSelf}"/> as its
/// <c>BindAsync</c> implementation.
/// </summary>
/// <remarks>
/// <para>Looks first at the form value with the matching field name; if empty, falls back to an
/// uploaded file with the same name and reads its bytes as JSON text — a convenience for browser
/// flows where the JSON payload arrives as a file picker rather than a text input.</para>
/// <para>This is intentionally the <i>only</i> binding logic in the plugin. Everything else
/// (<see cref="IFormFile"/>, <see cref="IFormFileCollection"/>, primitives, enums, etc.) is bound
/// natively by Minimal API through its standard parameter-binding rules — no need to duplicate
/// that work here.</para>
/// </remarks>
public static class JsonMultipartFormBinder
{
    public static async ValueTask<T?> BindJsonPartAsync<T>(HttpContext context, string fieldName)
        where T : class
    {
        if (!context.Request.HasFormContentType)
        {
            return null;
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);

        // Field present as a raw form value (string) — typical case from SPA / curl.
        var raw = form[fieldName].ToString();

        // Fall back to reading an uploaded file with the same name as JSON text — supports
        // browser file pickers shipping the JSON payload as a file rather than a string input.
        if (string.IsNullOrEmpty(raw))
        {
            var file = form.Files.GetFile(fieldName);
            if (file is null)
            {
                return null;
            }

            using var reader = new StreamReader(file.OpenReadStream());
            raw = await reader.ReadToEndAsync(context.RequestAborted);
        }

        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var jsonOpts = context.RequestServices.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions;
        return JsonSerializer.Deserialize<T>(raw, jsonOpts);
    }
}
