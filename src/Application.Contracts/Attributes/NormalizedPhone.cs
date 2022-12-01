using System.ComponentModel.DataAnnotations;
using PhoneNumbers;

namespace LayeredTemplate.Application.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
internal class NormalizedPhone : ValidationAttribute
{
    public NormalizedPhone(string errorMessage = "Invalid phone number.")
    {
        this.ErrorMessage = errorMessage;
    }

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

        var phoneNumberUtil = PhoneNumbers.PhoneNumberUtil.GetInstance();

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