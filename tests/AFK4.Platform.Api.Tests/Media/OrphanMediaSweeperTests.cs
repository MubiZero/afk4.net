using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Media;
using AFK4.Platform.Api.Tests.Fakes;
using AFK4.Platform.Api.Tests.Identity;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Media;

/// <summary>
/// Заменённые и брошенные картинки клуба уходят из хранилища, а всё, что где-то показывается, —
/// остаётся. Ошибка в сторону «стереть лишнее» хуже мусора: у игрока пропадёт фото зала.
/// </summary>
public sealed class OrphanMediaSweeperTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-28T12:00:00Z");
    private static readonly DateTimeOffset LastWeek = Now.AddDays(-7);

    [Fact]
    public async Task AReplacedNewsPhoto_Goes_TheOneStillShown_Stays()
    {
        await using var factory = new PlatformApiFactory();
        var replaced = await SeedMediaAsync(factory, MediaPurposeNames.NewsImage, LastWeek);
        var current = await SeedMediaAsync(factory, MediaPurposeNames.NewsImage, LastWeek);
        await WithDbAsync(factory, db => db.NewsItems.Add(new NewsItemEntity
        {
            Id = Guid.NewGuid(), OrganizationId = TestIds.OrganizationId, Title = "Турнир", Body = "В субботу",
            ImageUrl = current.PublicUrl, CreatedAtUtc = LastWeek, UpdatedAtUtc = LastWeek
        }));

        Assert.Equal(1, await SweepAsync(factory));

        var storage = Storage(factory);
        Assert.False(storage.Objects.ContainsKey(replaced.ObjectKey));
        Assert.True(storage.Objects.ContainsKey(current.ObjectKey));
        await WithDbAsync(factory, async db =>
            Assert.Equal(current.MediaId, Assert.Single(await db.UploadedMedia.Select(media => media.MediaId).ToListAsync())));
    }

    // Форма с новой картинкой может быть ещё открыта: свежую загрузку без ссылки не трогаем.
    [Fact]
    public async Task AFreshUploadWithoutAReference_WaitsOutTheGrace()
    {
        await using var factory = new PlatformApiFactory();
        var fresh = await SeedMediaAsync(factory, MediaPurposeNames.ProductImage, Now.AddHours(-2));

        Assert.Equal(0, await SweepAsync(factory));
        Assert.True(Storage(factory).Objects.ContainsKey(fresh.ObjectKey));
    }

    // Стенд без MinIO: настоящее хранилище с пустыми ключами раньше падало уже при создании, и
    // задача роняла каждый свой тик вместо того, чтобы молча пропустить его.
    [Fact]
    public async Task WithoutConfiguredStorage_TheTickIsSkipped_NotFailed()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var unconfigured = Options.Create(new MediaOptions());
        var sweeper = new OrphanMediaSweeper(
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
            new MinioMediaStorage(unconfigured),
            unconfigured,
            new MovableTimeProvider(Now));

        Assert.Equal(0, await sweeper.RunOnceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EverythingAClubShows_Stays_EvenUnderAnotherHost()
    {
        await using var factory = new PlatformApiFactory();
        var product = await SeedMediaAsync(factory, MediaPurposeNames.ProductImage, LastWeek);
        var logo = await SeedMediaAsync(factory, MediaPurposeNames.BranchLogo, LastWeek);
        var cover = await SeedMediaAsync(factory, MediaPurposeNames.BranchCover, LastWeek);
        var gallery = await SeedMediaAsync(factory, MediaPurposeNames.BranchGallery, LastWeek);
        var organizationLogo = await SeedMediaAsync(factory, MediaPurposeNames.OrganizationLogo, LastWeek);

        await WithDbAsync(factory, async db =>
        {
            db.PosProducts.Add(new PosProductEntity
            {
                ProductId = Guid.NewGuid(), OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId,
                CategoryId = Guid.NewGuid(), Name = "Кола", Sku = "cola", CurrencyCode = "TJS", PriceMinorUnits = 800,
                IsActive = true, CreatedAtUtc = LastWeek,
                // Хранилище сменило публичный домен: ссылка другая, файл тот же.
                ImageUrl = product.PublicUrl.Replace("https://media.test/", "https://cdn.afk4.test/media/")
            });
            db.Branches.Add(new BranchEntity
            {
                BranchId = TestIds.BranchId, OrganizationId = TestIds.OrganizationId, Slug = "rudaki", Name = "На Рудаки",
                City = "Душанбе", PreferredTimeZone = "Asia/Dushanbe", CreatedAtUtc = LastWeek,
                LogoUrl = logo.PublicUrl, CoverImageUrl = cover.PublicUrl,
                PhotosJson = BranchPhotos.Serialize([new BranchPhotoDto(gallery.PublicUrl, gallery.MediaId)])
            });
            var organization = await db.Organizations.SingleOrDefaultAsync(o => o.OrganizationId == TestIds.OrganizationId);
            if (organization is null)
            {
                db.Organizations.Add(new OrganizationEntity
                {
                    OrganizationId = TestIds.OrganizationId, Slug = "afk4-test", Name = "AFK4",
                    LogoUrl = organizationLogo.PublicUrl, CreatedAtUtc = LastWeek
                });
            }
            else
            {
                organization.LogoUrl = organizationLogo.PublicUrl;
            }
        });

        Assert.Equal(0, await SweepAsync(factory));
        // Хранилище-заглушка одно на все тесты хоста: считаем не всё, а свои пять файлов.
        var storage = Storage(factory);
        Assert.All(new[] { product, logo, cover, gallery, organizationLogo },
            media => Assert.True(storage.Objects.ContainsKey(media.ObjectKey), media.Purpose));
    }

    [Theory]
    [InlineData("https://media.test/org/branch/3f2c7d9e-1b4a-4c8e-9f00-5d6e7a8b9c0d.png", true)]
    [InlineData("https://media.test/org/branch/3f2c7d9e-1b4a-4c8e-9f00-5d6e7a8b9c0d.webp?v=2", true)]
    [InlineData("https://example.com/banner.png", false)]
    [InlineData("не ссылка", false)]
    [InlineData(null, false)]
    public void TheMediaIdIsReadFromTheFileName(string? url, bool found)
    {
        Assert.Equal(found, OrphanMediaSweeper.MediaIdOf(url) is not null);
    }

    private static async Task<UploadedMediaEntity> SeedMediaAsync(PlatformApiFactory factory, string purpose, DateTimeOffset createdAt)
    {
        var mediaId = Guid.NewGuid();
        var objectKey = $"{TestIds.OrganizationId}/{TestIds.BranchId}/{mediaId}.png";
        var storage = Storage(factory);
        using var bytes = new MemoryStream([1, 2, 3]);
        var url = await storage.PutAsync(objectKey, "image/png", bytes, CancellationToken.None);
        var entity = new UploadedMediaEntity
        {
            MediaId = mediaId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, Purpose = purpose,
            ObjectKey = objectKey, ContentType = "image/png", SizeBytes = 3, PublicUrl = url,
            CreatedByStaffUserId = Guid.NewGuid(), CreatedAtUtc = createdAt
        };
        await WithDbAsync(factory, db => db.UploadedMedia.Add(entity));
        return entity;
    }

    private static FakeMediaStorage Storage(PlatformApiFactory factory) =>
        (FakeMediaStorage)factory.Services.GetRequiredService<IMediaStorage>();

    private static async Task<int> SweepAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var sweeper = new OrphanMediaSweeper(
            services.GetRequiredService<PlatformDbContext>(),
            services.GetRequiredService<IMediaStorage>(),
            services.GetRequiredService<IOptions<MediaOptions>>(),
            new MovableTimeProvider(Now));
        return await sweeper.RunOnceAsync(CancellationToken.None);
    }

    private static Task WithDbAsync(PlatformApiFactory factory, Action<PlatformDbContext> change) =>
        WithDbAsync(factory, db =>
        {
            change(db);
            return Task.CompletedTask;
        });

    private static async Task WithDbAsync(PlatformApiFactory factory, Func<PlatformDbContext, Task> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await change(db);
        await db.SaveChangesAsync();
    }
}
