using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class UploadedMediaEntityTests
{
    [Fact]
    public async Task PersistsAndReadsBack()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"media-{Guid.NewGuid()}").Options;
        await using var db = new PlatformDbContext(options);
        var id = Guid.NewGuid();
        db.UploadedMedia.Add(new UploadedMediaEntity
        {
            MediaId = id, OrganizationId = Guid.NewGuid(), BranchId = Guid.NewGuid(),
            Purpose = "branch-logo", ObjectKey = "o/b/x.png", ContentType = "image/png",
            SizeBytes = 123, PublicUrl = "https://minio/x.png",
            CreatedByStaffUserId = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        await db.SaveChangesAsync();

        var read = await db.UploadedMedia.AsNoTracking().SingleAsync(m => m.MediaId == id);
        Assert.Equal("branch-logo", read.Purpose);
        Assert.Equal(123, read.SizeBytes);
    }
}
