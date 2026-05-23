using System.Text.RegularExpressions;

namespace AFK4.Platform.Api.Platform.Tenancy;

public static class SlugValidator
{
    public const int MinLength = 3;
    public const int MaxLength = 64;

    private static readonly Regex Pattern = new(
        @"^[a-z0-9]+(-[a-z0-9]+)*$",
        RegexOptions.Compiled);

    public static string Normalize(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    public static string? Validate(string normalized, string fieldName)
    {
        if (string.IsNullOrEmpty(normalized))
        {
            return $"{fieldName} is required.";
        }

        if (normalized.Length < MinLength)
        {
            return $"{fieldName} must be at least {MinLength} characters long.";
        }

        if (normalized.Length > MaxLength)
        {
            return $"{fieldName} must be at most {MaxLength} characters long.";
        }

        if (!Pattern.IsMatch(normalized))
        {
            return $"{fieldName} must contain only lowercase letters, digits, and single hyphens between alphanumeric segments.";
        }

        return null;
    }
}
