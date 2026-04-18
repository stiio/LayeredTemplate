namespace LayeredTemplate.Auth.Web.Infrastructure.Identity;

/// <summary>
/// Parameters for the <c>Invite</c> token provider, used to generate one-time links
/// that let a newly-provisioned user set their password and sign in.
///
/// The token stores the user's security stamp; accepting the invite rotates the stamp
/// (via <c>AddPasswordAsync</c>) which invalidates the token — giving us single-use
/// semantics on top of the TTL.
/// </summary>
public static class InviteTokenSettings
{
    public static readonly TimeSpan Lifespan = TimeSpan.FromDays(30);

    public const string ProviderName = "Invite";

    public const string Purpose = "AcceptInvite";
}
