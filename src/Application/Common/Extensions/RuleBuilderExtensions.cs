using FluentValidation;
using PhoneNumbers;

namespace LayeredTemplate.Application.Common.Extensions;

public static class RuleBuilderExtensions
{
    public static IRuleBuilderOptions<T, string> NormalizedPhone<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Must((rootObject, src, context) =>
            {
                if (src == null)
                {
                    return true;
                }

                var phoneNumberUtil = PhoneNumberUtil.GetInstance();

                PhoneNumber phone;
                try
                {
                    phone = phoneNumberUtil.Parse(src, null);
                }
                catch
                {
                    return false;
                }

                return phoneNumberUtil.IsValidNumber(phone);
            })
            .WithMessage("'{PropertyName}' must be a phone in E164 format.");
    }
}