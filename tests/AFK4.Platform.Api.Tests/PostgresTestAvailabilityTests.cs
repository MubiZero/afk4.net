using System.Reflection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Postgres-backed tests skip themselves when no test database is configured. That is right on a
/// laptop and wrong in CI: a pipeline whose database configuration broke would stay green while
/// silently testing nothing. Setting <see cref="RequireVariable"/> turns that silence into a failure.
/// </summary>
public sealed class PostgresTestAvailabilityTests
{
    public const string RequireVariable = "AFK4_REQUIRE_POSTGRES_TESTS";

    [Fact]
    public void ExternallyConfiguredFactAttributes_AreDiscoverable()
    {
        // Without this the guard below would pass by finding nothing at all, which is the very
        // failure mode it exists to prevent.
        Assert.NotEmpty(ExternallyConfiguredFactAttributes());
    }

    [Fact]
    public void ExternallyConfiguredFactAttributes_DoNotSkip_WhenTheirBackingServiceIsRequired()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RequireVariable), "1", StringComparison.Ordinal))
        {
            return;
        }

        var skipped = ExternallyConfiguredFactAttributes()
            .Select(type => (Type: type, Attribute: (FactAttribute)Activator.CreateInstance(type)!))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Attribute.Skip))
            .Select(candidate => $"  {candidate.Type.Name}: {candidate.Attribute.Skip}")
            .ToList();

        Assert.True(
            skipped.Count == 0,
            $"{RequireVariable}=1 demands that every externally configured test actually run, but "
                + $"{skipped.Count} attribute(s) would skip their tests:{Environment.NewLine}"
                + string.Join(Environment.NewLine, skipped));
    }

    /// <summary>
    /// Fact attributes that decide whether to skip based on an environment variable, found by
    /// reflection rather than by a hand-kept list: a list would silently miss the next one added.
    /// </summary>
    private static IReadOnlyList<Type> ExternallyConfiguredFactAttributes() =>
        typeof(PostgresTestAvailabilityTests).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract
                && typeof(FactAttribute).IsAssignableFrom(type)
                && type.GetField("EnvironmentVariable", BindingFlags.Public | BindingFlags.Static)
                    is { IsLiteral: true, FieldType: var fieldType } && fieldType == typeof(string))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();
}
