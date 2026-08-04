using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using PulsoIO.Modules.Identity.Domain;

namespace PulsoIO.Modules.Identity.Authentication;

internal sealed class SpecialCharacterPasswordValidator : IPasswordValidator<User>
{
    public Task<IdentityResult> ValidateAsync(
        UserManager<User> manager,
        User user,
        string? password)
    {
        if (!string.IsNullOrEmpty(password) &&
            password.EnumerateRunes().Any(IsPunctuationOrSymbol))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = "PasswordRequiresSpecialCharacter",
            Description = "A senha deve conter pelo menos um caractere de pontuação ou símbolo."
        }));
    }

    private static bool IsPunctuationOrSymbol(Rune rune)
    {
        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.DashPunctuation or
            UnicodeCategory.OpenPunctuation or
            UnicodeCategory.ClosePunctuation or
            UnicodeCategory.InitialQuotePunctuation or
            UnicodeCategory.FinalQuotePunctuation or
            UnicodeCategory.OtherPunctuation or
            UnicodeCategory.MathSymbol or
            UnicodeCategory.CurrencySymbol or
            UnicodeCategory.ModifierSymbol or
            UnicodeCategory.OtherSymbol;
    }
}
