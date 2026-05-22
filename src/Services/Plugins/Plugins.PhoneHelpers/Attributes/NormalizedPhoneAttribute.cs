using System.ComponentModel.DataAnnotations;

namespace LayeredTemplate.Plugins.PhoneHelpers.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class NormalizedPhoneAttribute : ValidationAttribute
{
    /// <summary>
    /// NormalizedPhone
    /// </summary>
    /// <param name="errorMessage"></param>
    public NormalizedPhoneAttribute(string errorMessage = "Invalid phone number.")
    {
        this.ErrorMessage = errorMessage;
    }

    /// <summary>
    /// IsValid
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string s)
        {
            return false;
        }

        // Whitespace-only value is treated as invalid here (legacy semantics from earlier
        // Auth.Web Edit/Create flows). For "optional phone" callers, use a nullable property
        // so the value comes through as null instead of "".
        return PhoneNumberValidator.IsValid(s);
    }
}