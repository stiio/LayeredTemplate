using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.JsonMultipart.Integrations;

internal class JsonModelBinder : IModelBinder
{
    private readonly IOptions<JsonOptions> jsonOptions;

    public JsonModelBinder(IOptions<JsonOptions> jsonOptions)
    {
        this.jsonOptions = jsonOptions;
    }

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
        {
            throw new ArgumentNullException(nameof(bindingContext));
        }

        var modelBindingKey = bindingContext.IsTopLevelObject ? bindingContext.BinderModelName! : bindingContext.ModelName;

        // Check the value sent in
        var valueProviderResult = await this.GetValueProvidedResult(bindingContext).ConfigureAwait(false);
        if (valueProviderResult == ValueProviderResult.None)
        {
            return;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        // Attempt to convert the input value
        var valueAsString = valueProviderResult.FirstValue;
        try
        {
            if (string.IsNullOrEmpty(valueAsString))
            {
                return;
            }

            var result = this.DeserializeUsingSystemSerializer(bindingContext, valueAsString!);

            bindingContext.Result = ModelBindingResult.Success(result);
        }
        catch (Exception e)
        {
            bindingContext.ModelState.AddModelError(modelBindingKey ?? string.Empty, e.Message);
        }
    }

    private object? DeserializeUsingSystemSerializer(ModelBindingContext bindingContext, string valueAsString)
    {
        return JsonSerializer.Deserialize(valueAsString, bindingContext.ModelType, this.jsonOptions?.Value?.JsonSerializerOptions);
    }

    private async Task<ValueProviderResult> GetValueProvidedResult(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult != ValueProviderResult.None)
        {
            return valueProviderResult;
        }

        var file = bindingContext.HttpContext.Request.Form.Files.GetFile(bindingContext.ModelName);
        if (file is null)
        {
            return valueProviderResult;
        }

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync().ConfigureAwait(false);
        valueProviderResult = new ValueProviderResult(text);

        return valueProviderResult;
    }
}