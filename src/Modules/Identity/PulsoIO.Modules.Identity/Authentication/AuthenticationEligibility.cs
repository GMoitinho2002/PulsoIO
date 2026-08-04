using PulsoIO.Modules.Identity.Domain;

namespace PulsoIO.Modules.Identity.Authentication;

internal static class AuthenticationEligibility
{
    public static bool IsAllowed(User user)
    {
        return user.IsActive;
    }
}
