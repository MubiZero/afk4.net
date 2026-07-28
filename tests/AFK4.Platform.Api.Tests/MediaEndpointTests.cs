using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Media;
using AFK4.Platform.Api.Tests.Fakes;
using AFK4.Shared.Contracts.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class MediaEndpointTests
{
    private static MultipartFormDataContent PngForm(string purpose = "branch-logo")
    {
        var content = new MultipartFormDataContent();
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new("application/octet-stream");
        content.Add(file, "file", "logo.png");
        content.Add(new StringContent(purpose), "purpose");
        return content;
    }

    private static MultipartFormDataContent PdfForm(string purpose = "branch-logo")
    {
        var content = new MultipartFormDataContent();
        var bytes = "%PDF-1.4 not really an image"u8.ToArray();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new("application/octet-stream");
        content.Add(file, "file", "doc.pdf");
        content.Add(new StringContent(purpose), "purpose");
        return content;
    }

    [Fact]
    public async Task Post_WithoutToken_Unauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/branches/{TestIds.BranchId:D}/media", PngForm());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutPermission_Forbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        var response = await client.PostAsync($"/api/branches/{TestIds.BranchId:D}/media", PngForm());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_Png_Ok_ReturnsUrlAndPersists()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PostAsync($"/api/branches/{TestIds.BranchId:D}/media", PngForm());
        var body = await response.Content.ReadFromJsonAsync<UploadedMediaDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.Url));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persisted = await dbContext.UploadedMedia
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.MediaId == body.MediaId);
        Assert.NotNull(persisted);
        Assert.Equal(TestIds.BranchId, persisted.BranchId);

        var storage = (FakeMediaStorage)factory.Services.GetRequiredService<IMediaStorage>();
        Assert.Single(storage.Objects);
    }

    [Fact]
    public async Task Post_NonImage_BadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PostAsync($"/api/branches/{TestIds.BranchId:D}/media", PdfForm());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_MismatchedBranchOfAnotherOrg_Forbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PostAsync($"/api/branches/{TestIds.OtherBranchId:D}/media", PngForm());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Ok_NoContent()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var uploadResponse = await client.PostAsync($"/api/branches/{TestIds.BranchId:D}/media", PngForm());
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<UploadedMediaDto>();
        Assert.NotNull(uploaded);

        var deleteResponse = await client.DeleteAsync($"/api/branches/{TestIds.BranchId:D}/media/{uploaded.MediaId:D}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persisted = await dbContext.UploadedMedia.AsNoTracking().SingleOrDefaultAsync(m => m.MediaId == uploaded.MediaId);
        Assert.Null(persisted);
    }
}
