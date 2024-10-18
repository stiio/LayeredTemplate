using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LayeredTemplate.Shared.Options;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.App.Web.Json.TypeResolvers;

public class PolymorphicTypeResolver : DefaultJsonTypeInfoResolver
{
    private readonly JsonPolymorphismSettings jsonPolymorphismSettings;

    public PolymorphicTypeResolver(IOptions<JsonPolymorphismSettings> jsonPolymorphismSettings)
    {
        this.jsonPolymorphismSettings = jsonPolymorphismSettings.Value;
    }

    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

        var assemblies = this.jsonPolymorphismSettings.Assemblies;

        var childTypes = assemblies.SelectMany(x => x.GetTypes())
            .Where(x => x.IsAssignableTo(type) && !x.IsAbstract)
            .ToArray();

        if (childTypes.Length < 1 || (childTypes.Length == 1 && childTypes[0] == type))
        {
            return jsonTypeInfo;
        }

        jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
        {
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            DerivedTypes =
            {
            },
        };

        foreach (var childType in childTypes)
        {
            jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(childType, childType.Name));
        }

        return jsonTypeInfo;
    }
}