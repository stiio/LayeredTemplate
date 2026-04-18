using System.ComponentModel.DataAnnotations;
using PhoneNumbers;

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

        if (value is not string valueAsString)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(valueAsString))
        {
            return false;
        }

        var phoneNumberUtil = PhoneNumberUtil.GetInstance();

        PhoneNumber phone;
        try
        {
            phone = phoneNumberUtil.Parse(valueAsString, null);
        }
        catch
        {
            return false;
        }

        return phoneNumberUtil.IsValidNumber(phone);
    }
}