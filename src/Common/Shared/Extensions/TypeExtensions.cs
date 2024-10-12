namespace LayeredTemplate.Shared.Extensions;

public static class TypeExtensions
{
    public static Type GetRootBaseType(this Type type)
    {
        while (true)
        {
            if (type.BaseType is null || type.BaseType == typeof(object))
            {
                return type;
            }

            type = type.BaseType;
        }
    }
}