namespace LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;

public class SigningCredential
{
    public Guid Id { get; set; }

    /// <summary>RSA key parameters serialized as JSON.</summary>
    public string KeyData { get; set; } = default!;

    /// <summary>Purpose: "signing" or "encryption".</summary>
    public string Purpose { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
}
