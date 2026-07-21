using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Media;
using AFK4.Platform.Api.Tests.Fakes;
using AFK4.Shared.Contracts.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests;

public sealed class EfMediaServiceTests
{
    private const long MaxBytes = 10 * 1024 * 1024;

    private static Stream Png() => new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 });

    private static Stream NonImage() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }); // "%PDF-1.4"

    [Fact]
    public async Task Upload_Png_StoresObjectAndRecord()
    {
        await using var db = CreateDbContext();
        var storage = new FakeMediaStorage();
        var service = CreateService(db, storage);

        var result = await service.UploadAsync(
            TestIds.OrganizationId, TestIds.BranchId, TestIds.TechnicianStaffUserId,
            MediaPurposeNames.BranchLogo, "image/png", Png(), 12, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.NotNull(result.Media);
        Assert.False(string.IsNullOrEmpty(result.Media!.Url));
        Assert.Equal("image/png", result.Media.ContentType);
        Assert.Single(storage.Objects);
        Assert.Equal(1, await db.UploadedMedia.CountAsync());
    }

    [Fact]
    public async Task Upload_OverMaxBytes_Rejected()
    {
        await using var db = CreateDbContext();
        var storage = new FakeMediaStorage();
        var service = CreateService(db, storage);

        var result = await service.UploadAsync(
            TestIds.OrganizationId, TestIds.BranchId, TestIds.TechnicianStaffUserId,
            MediaPurposeNames.BranchLogo, "image/png", Png(), MaxBytes + 1, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Media);
        Assert.Contains("size", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(storage.Objects);
        Assert.Equal(0, await db.UploadedMedia.CountAsync());
    }

    [Fact]
    public async Task Upload_NonImageMagicBytes_Rejected()
    {
        await using var db = CreateDbContext();
        var storage = new FakeMediaStorage();
        var service = CreateService(db, storage);

        var result = await service.UploadAsync(
            TestIds.OrganizationId, TestIds.BranchId, TestIds.TechnicianStaffUserId,
            MediaPurposeNames.BranchLogo, "application/pdf", NonImage(), 8, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Media);
        Assert.False(string.IsNullOrEmpty(result.Error));
        Assert.Empty(storage.Objects);
        Assert.Equal(0, await db.UploadedMedia.CountAsync());
    }

    [Fact]
    public async Task Upload_SecondBranchLogo_DeletesPrevious()
    {
        await using var db = CreateDbContext();
        var storage = new FakeMediaStorage();
        var service = CreateService(db, storage);

        var first = await service.UploadAsync(
            TestIds.OrganizationId, TestIds.BranchId, TestIds.TechnicianStaffUserId,
            MediaPurposeNames.BranchLogo, "image/png", Png(), 12, CancellationToken.None);
        var second = await service.UploadAsync(
            TestIds.OrganizationId, TestIds.BranchId, TestIds.TechnicianStaffUserId,
            MediaPurposeNames.BranchLogo, "image/png", Png(), 12, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotEqual(first.Media!.MediaId, second.Media!.MediaId);
        Assert.Single(storage.Objects);
        Assert.Equal(1, await db.UploadedMedia.CountAsync());
        var remaining = await db.UploadedMedia.SingleAsync();
        Assert.Equal(second.Media.MediaId, remaining.MediaId);
    }

    [Fact]
    public async Task Delete_RemovesObjectAndRecord()
    {
        await using var db = CreateDbContext();
        var storage = new FakeMediaStorage();
        var service = CreateService(db, storage);

        var uploaded = await service.UploadAsync(
            TestIds.OrganizationId, TestIds.BranchId, TestIds.TechnicianStaffUserId,
            MediaPurposeNames.BranchLogo, "image/png", Png(), 12, CancellationToken.None);

        var deleted = await service.DeleteAsync(
            TestIds.OrganizationId, TestIds.BranchId, uploaded.Media!.MediaId, CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(storage.Objects);
        Assert.Equal(0, await db.UploadedMedia.CountAsync());
    }

    private static EfMediaService CreateService(PlatformDbContext db, FakeMediaStorage storage) =>
        new(db, storage, Options.Create(new MediaOptions { MaxBytes = MaxBytes }), TimeProvider.System);

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }
}
