using System.Text;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Normalizes a human-typed phone number to E.164 digits-only form (no '+', spaces, dashes,
/// or parentheses), e.g. "+992 93 738-00-70" -> "992937380070". Requires a country code:
/// we serve the CIS market (+992/+7/+998 = 11–12 digits), so a bare local number is rejected.
/// Returns null when the input is missing or not a plausible international number.
/// </summary>
public static class PhoneNumberNormalizer
{
    // E.164 allows up to 15 digits; require a country code so we never store ambiguous locals.
    private const int MinDigits = 11;
    private const int MaxDigits = 15;

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var character in raw)
        {
            if (character >= '0' && character <= '9')
            {
                builder.Append(character);
            }
        }

        return builder.Length is >= MinDigits and <= MaxDigits ? builder.ToString() : null;
    }
}
