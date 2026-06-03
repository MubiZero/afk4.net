using System;
using AFK4.Platform.Api.Common;

namespace AFK4.Platform.Api.Tests;

public class CursorTokenTests
{
    [Fact]
    public void EncodeThenDecode_RoundTripsTimestampAndId()
    {
        var ts = DateTimeOffset.Parse("2026-06-03T12:34:56.789Z");
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var encoded = CursorToken.Encode(ts, id);
        var ok = CursorToken.TryDecode(encoded, out var decodedTs, out var decodedId);

        Assert.True(ok);
        Assert.Equal(ts, decodedTs);
        Assert.Equal(id, decodedId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64-!!!")]
    [InlineData("YWJj")] // valid base64, wrong shape
    public void TryDecode_OnGarbage_ReturnsFalse(string garbage)
    {
        var ok = CursorToken.TryDecode(garbage, out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryDecode_OnNull_ReturnsFalse()
    {
        var ok = CursorToken.TryDecode(null, out _, out _);
        Assert.False(ok);
    }
}
