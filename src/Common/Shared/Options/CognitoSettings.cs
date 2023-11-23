namespace LayeredTemplate.Shared.Options;

public class CognitoSettings
{
    public string UserPoolId { get; set; } = null!;

    public string Audience { get; set; } = null!;
}