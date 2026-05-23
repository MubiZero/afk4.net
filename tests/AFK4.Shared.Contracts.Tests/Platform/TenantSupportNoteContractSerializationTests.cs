using System.Text.Json;
using AFK4.Shared.Contracts.Platform.SupportNotes;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class TenantSupportNoteContractSerializationTests
{
    [Fact]
    public void TenantSupportNoteDto_RoundTripsThroughJson()
    {
        var note = new TenantSupportNoteDto(
            TenantSupportNoteId: Guid.Parse("12345678-1234-1234-1234-123456789012"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            AuthorPlatformAdminId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
            AuthorDisplayName: "Support Admin",
            Body: "Owner asked to extend trial by one week.",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T09:00:00Z"));

        var json = JsonSerializer.Serialize(note);
        var copy = JsonSerializer.Deserialize<TenantSupportNoteDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(note.TenantSupportNoteId, copy.TenantSupportNoteId);
        Assert.Equal(note.OrganizationId, copy.OrganizationId);
        Assert.Equal(note.AuthorPlatformAdminId, copy.AuthorPlatformAdminId);
        Assert.Equal(note.Body, copy.Body);
    }

    [Fact]
    public void CreateTenantSupportNoteRequest_RoundTripsThroughJson()
    {
        var request = new CreateTenantSupportNoteRequest("New note text");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<CreateTenantSupportNoteRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.Body, copy.Body);
    }
}
