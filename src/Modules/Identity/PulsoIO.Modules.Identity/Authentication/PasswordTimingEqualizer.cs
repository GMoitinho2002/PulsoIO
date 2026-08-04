using Microsoft.AspNetCore.Identity;
using PulsoIO.Modules.Identity.Domain;

namespace PulsoIO.Modules.Identity.Authentication;

internal static class PasswordTimingEqualizer
{
    private static readonly User DummyUser = new("Usuário", "invalid@pulso.local");
    private static readonly PasswordHasher<User> Hasher = new();
    private static readonly string DummyHash = Hasher.HashPassword(DummyUser, Guid.NewGuid().ToString("N"));

    public static void Verify(string password)
    {
        _ = Hasher.VerifyHashedPassword(DummyUser, DummyHash, password);
    }
}
