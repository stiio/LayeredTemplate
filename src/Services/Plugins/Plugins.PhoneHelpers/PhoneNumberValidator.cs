using PhoneNumbers;

namespace LayeredTemplate.Plugins.PhoneHelpers;

public static class PhoneNumberValidator
{
    /// <summary>
    /// True when <paramref name="value"/> parses cleanly via libphonenumber AND
    /// <see cref="PhoneNumberUtil.IsValidNumber(PhoneNumber)"/> says it's a real number for
    /// the inferred region. Null / whitespace input returns <c>false</c> — callers that want
    /// to allow empty values should short-circuit before calling.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var util = PhoneNumberUtil.GetInstance();

        PhoneNumber phone;
        try
        {
            phone = util.Parse(value, defaultRegion: null);
        }
        catch
        {
            // libphonenumber throws NumberParseException on malformed input; treat as invalid
            // rather than letting the exception escape into validation pipelines.
            return false;
        }

        return util.IsValidNumber(phone);
    }
}