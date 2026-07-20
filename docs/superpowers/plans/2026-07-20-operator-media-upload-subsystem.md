# Медиа-загрузка (MinIO) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать приложению загрузку пользовательских изображений из браузера (первый потребитель — логотип клуба), храня файлы в MinIO (server-mediated upload через Platform.Api), с реестром для lifecycle и переиспользуемым UI-компонентом.

**Architecture:** Platform.Api получает multipart от аутентифицированного сотрудника → валидирует (размер + magic-byte) → кладёт объект в media-бакет MinIO через `IMediaStorage` (реализация на `AWSSDK.S3`) → пишет запись `UploadedMediaEntity` → возвращает публичный URL. Браузер грузит картинку напрямую из public-read MinIO по URL. В тестах `IMediaStorage` подменяется фейком (как `ISessionBillingService`).

**Tech Stack:** .NET 10 (C#, minimal APIs, EF Core + Npgsql, xUnit + WebApplicationFactory + EF InMemory), `AWSSDK.S3` (MinIO через ServiceURL+ForcePathStyle), React + TS + `bun test` (фронт).

## Global Constraints

- **Секреты только через env**, никогда в коде/репо/логах. MinIO-креды читаются из конфигурации (`Media:S3:AccessKey`/`SecretKey`), реальные значения — из env (`Media__S3__AccessKey` и т.п.), в `appsettings.json` — пустые дефолты (паттерн `Secrets`).
- **IDOR-guard обязателен:** любой branch-эндпоинт через `StaffAuthorizationService.RequireBranchPermissionAsync(branchId, permission, ct)` (проверяет org-принадлежность филиала + назначение + право). Тело с `OrganizationId` (если есть) сверять со `StaffContext.OrganizationId`.
- **Миграция EF (Linux-safe recipe):** сперва `dotnet build src/AFK4.Platform.Api`, затем `dotnet ef migrations add <Name> --project src/AFK4.Platform.Api --output-dir Data/Migrations --no-build`. НЕ `dotnet ef migrations remove` (лезет в Postgres) — откат = `rm` .cs/.Designer.cs + проверить, что `PlatformDbContextModelSnapshot.cs` не осиротел. Если ef-тула нет: `dotnet tool restore`. Миграции аддитивные (nullable/defaulted колонки).
- **Deploy-пререквизит (вне кода):** миграция, тронувшая `Data/Migrations/**`, блокирует merge в Coolify-воркфлоу до применения на staging DB (`workflow_dispatch` с `confirm_migrations_applied=true`). Плюс провижн media-бакета `afk4-media[-staging]` (public-read policy) в MinIO. Оба — ручные ops-шаги, отметить в отчёте, НЕ пытаться выполнить из кода.
- **Валидация авторитетна на сервере:** размер ≤ `Media:MaxBytes` (дефолт 10 МБ = 10485760) и тип по **magic-byte sniff** (не по Content-Type заголовку). Разрешено: png/jpeg/webp. SVG — вне скоупа v1.
- **i18n (фронт):** строки только через `@afk4/i18n`, источник `/locales/{ru,en,tg}.json` + `cd packages/i18n && bun run gen` (messages.ts генерируется); ru/en/tg паритет; tg — настоящий таджикский, НЕ копия ru.
- **Гейты:** backend — `dotnet test tests/AFK4.Platform.Api.Tests` зелёный + `dotnet build`; фронт — `bun test` + `bun run build` (тайпчекает тесты) зелёные. Коммит в конце каждой задачи.
- Деньги/время — не затрагиваются.

---

## Task 1: Data-слой — контракты, сущность, миграция

**Files:**
- Create: `src/AFK4.Shared.Contracts/Media/UploadedMediaDto.cs`
- Create: `src/AFK4.Shared.Contracts/Media/MediaPurposeNames.cs`
- Create: `src/AFK4.Platform.Api/Data/UploadedMediaEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` (DbSet + OnModelCreating блок)
- Create (генерится): `src/AFK4.Platform.Api/Data/Migrations/<ts>_AddUploadedMedia.cs`
- Test: `tests/AFK4.Platform.Api.Tests/UploadedMediaEntityTests.cs`

**Interfaces:**
- Produces: `UploadedMediaEntity` (поля ниже), `UploadedMediaDto(Guid MediaId, string Url, string ContentType, long SizeBytes)`, `MediaPurposeNames.BranchLogo = "branch-logo"`. Task 3 использует сущность; Task 5/UI использует DTO.

- [ ] **Step 1: Контракты**

`src/AFK4.Shared.Contracts/Media/UploadedMediaDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.Media;

public sealed record UploadedMediaDto(
    Guid MediaId,
    string Url,
    string ContentType,
    long SizeBytes);
```

`src/AFK4.Shared.Contracts/Media/MediaPurposeNames.cs`:
```csharp
namespace AFK4.Shared.Contracts.Media;

public static class MediaPurposeNames
{
    public const string BranchLogo = "branch-logo";
    // news-image добавится, когда Новости перейдут на upload (вне этого под-проекта)
}
```

- [ ] **Step 2: Сущность**

`src/AFK4.Platform.Api/Data/UploadedMediaEntity.cs`:
```csharp
namespace AFK4.Platform.Api.Data;

public sealed class UploadedMediaEntity
{
    public Guid MediaId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string PublicUrl { get; set; } = string.Empty;
    public Guid CreatedByStaffUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

- [ ] **Step 3: DbContext — DbSet + конфиг**

В `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` добавить DbSet рядом с прочими (напр. после `NewsItems`):
```csharp
public DbSet<UploadedMediaEntity> UploadedMedia => Set<UploadedMediaEntity>();
```
И блок в `OnModelCreating` (рядом с `NewsItemEntity`):
```csharp
modelBuilder.Entity<UploadedMediaEntity>(entity =>
{
    entity.ToTable("uploaded_media");
    entity.HasKey(media => media.MediaId);
    entity.Property(media => media.Purpose).HasMaxLength(64).IsRequired();
    entity.Property(media => media.ObjectKey).HasMaxLength(512).IsRequired();
    entity.Property(media => media.ContentType).HasMaxLength(128).IsRequired();
    entity.Property(media => media.PublicUrl).HasMaxLength(2048).IsRequired();
    entity.HasIndex(media => new { media.OrganizationId, media.BranchId, media.Purpose });
});
```

- [ ] **Step 4: Собрать и создать миграцию (Linux-safe)**

Run:
```bash
dotnet build src/AFK4.Platform.Api
dotnet ef migrations add AddUploadedMedia --project src/AFK4.Platform.Api --output-dir Data/Migrations --no-build
```
Expected: новый `<ts>_AddUploadedMedia.cs` с непустыми `Up`/`Down` (CreateTable `uploaded_media` + индекс). Если `Up` пуст — забыл `dotnet build` перед add: удалить .cs/.Designer.cs, пересобрать, повторить. Открыть миграцию, убедиться: `CreateTable("uploaded_media", ...)` со всеми колонками snake_case + `CreateIndex`.

- [ ] **Step 5: Тест маппинга сущности**

`tests/AFK4.Platform.Api.Tests/UploadedMediaEntityTests.cs`:
```csharp
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
```

- [ ] **Step 6: Прогнать backend-тесты и сборку**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~UploadedMediaEntityTests`
Expected: PASS. Затем `dotnet build src/AFK4.Platform.Api` — успешно.

- [ ] **Step 7: Коммит**
```bash
git add src/AFK4.Shared.Contracts/Media src/AFK4.Platform.Api/Data/UploadedMediaEntity.cs \
        src/AFK4.Platform.Api/Data/PlatformDbContext.cs src/AFK4.Platform.Api/Data/Migrations \
        tests/AFK4.Platform.Api.Tests/UploadedMediaEntityTests.cs
git commit -m "feat(media): uploaded_media entity, contracts, migration"
```

---

## Task 2: Хранилище — `IMediaStorage` + MinIO (AWSSDK.S3) + конфиг + фейк

**Files:**
- Create: `src/AFK4.Platform.Api/Media/IMediaStorage.cs`
- Create: `src/AFK4.Platform.Api/Media/MinioMediaStorage.cs`
- Create: `src/AFK4.Platform.Api/Media/MediaOptions.cs`
- Modify: `src/AFK4.Platform.Api/AFK4.Platform.Api.csproj` (пакет `AWSSDK.S3`)
- Modify: `src/AFK4.Platform.Api/Program.cs` (Configure<MediaOptions> + AddScoped<IMediaStorage>)
- Modify: `src/AFK4.Platform.Api/appsettings.json` (пустой `Media` stanza)
- Test: `tests/AFK4.Platform.Api.Tests/Fakes/FakeMediaStorage.cs`

**Interfaces:**
- Produces:
```csharp
public interface IMediaStorage
{
    // Кладёт объект, возвращает публичный URL. objectKey формирует вызывающий.
    Task<string> PutAsync(string objectKey, string contentType, Stream content, CancellationToken ct);
    Task DeleteAsync(string objectKey, CancellationToken ct);
    string PublicUrlFor(string objectKey);
}
```
  Task 3 (EfMediaService) потребляет `IMediaStorage`. `MediaOptions.MaxBytes` тоже читает Task 3/4.

- [ ] **Step 1: Пакет AWSSDK.S3**

В `src/AFK4.Platform.Api/AFK4.Platform.Api.csproj` добавить в `<ItemGroup>` с PackageReference:
```xml
<PackageReference Include="AWSSDK.S3" Version="3.7.416.15" />
```
(Если версия недоступна — взять последнюю 3.7.x, зафиксировать в отчёте.) Run: `dotnet restore src/AFK4.Platform.Api`.

- [ ] **Step 2: Опции**

`src/AFK4.Platform.Api/Media/MediaOptions.cs`:
```csharp
namespace AFK4.Platform.Api.Media;

public sealed class MediaOptions
{
    public const string SectionName = "Media";
    public S3Options S3 { get; set; } = new();
    public long MaxBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    public sealed class S3Options
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string PublicBaseUri { get; set; } = string.Empty;
        public string Region { get; set; } = "us-east-1";
    }
}
```

- [ ] **Step 3: Интерфейс + MinIO-реализация**

`src/AFK4.Platform.Api/Media/IMediaStorage.cs` — интерфейс из блока Interfaces выше.

`src/AFK4.Platform.Api/Media/MinioMediaStorage.cs`:
```csharp
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Media;

public sealed class MinioMediaStorage : IMediaStorage
{
    private readonly MediaOptions.S3Options s3;
    private readonly IAmazonS3 client;

    public MinioMediaStorage(IOptions<MediaOptions> options)
    {
        s3 = options.Value.S3;
        var config = new AmazonS3Config
        {
            ServiceURL = s3.Endpoint,
            ForcePathStyle = true,               // MinIO: path-style bucket addressing
            AuthenticationRegion = s3.Region
        };
        client = new AmazonS3Client(s3.AccessKey, s3.SecretKey, config);
    }

    public async Task<string> PutAsync(string objectKey, string contentType, Stream content, CancellationToken ct)
    {
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = s3.Bucket,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);
        return PublicUrlFor(objectKey);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken ct)
        => await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = s3.Bucket, Key = objectKey }, ct);

    public string PublicUrlFor(string objectKey)
        => $"{s3.PublicBaseUri.TrimEnd('/')}/{objectKey}";
}
```
(Public-read настраивается bucket-policy на стороне MinIO — per-object ACL не ставим.)

- [ ] **Step 4: DI + appsettings**

В `src/AFK4.Platform.Api/Program.cs` рядом с `Configure<SecretProtectionOptions>` (около :314):
```csharp
builder.Services.Configure<MediaOptions>(builder.Configuration.GetSection(MediaOptions.SectionName));
builder.Services.AddScoped<IMediaStorage, MinioMediaStorage>();
```
В `src/AFK4.Platform.Api/appsettings.json` добавить пустой stanza:
```json
"Media": {
  "S3": { "Endpoint": "", "Bucket": "", "AccessKey": "", "SecretKey": "", "PublicBaseUri": "", "Region": "us-east-1" },
  "MaxBytes": 10485760
}
```

- [ ] **Step 5: Фейк для тестов**

`tests/AFK4.Platform.Api.Tests/Fakes/FakeMediaStorage.cs`:
```csharp
using System.Collections.Concurrent;
using AFK4.Platform.Api.Media;

public sealed class FakeMediaStorage : IMediaStorage
{
    public readonly ConcurrentDictionary<string, byte[]> Objects = new();

    public async Task<string> PutAsync(string objectKey, string contentType, Stream content, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        Objects[objectKey] = ms.ToArray();
        return PublicUrlFor(objectKey);
    }

    public Task DeleteAsync(string objectKey, CancellationToken ct)
    { Objects.TryRemove(objectKey, out _); return Task.CompletedTask; }

    public string PublicUrlFor(string objectKey) => $"https://media.test/{objectKey}";
}
```

- [ ] **Step 6: Сборка**

Run: `dotnet build src/AFK4.Platform.Api` и `dotnet build tests/AFK4.Platform.Api.Tests`
Expected: успешно (реальный MinIO не дёргаем — `MinioMediaStorage` покрывается staging-смоуком, юнит-путь идёт через `FakeMediaStorage`).

- [ ] **Step 7: Коммит**
```bash
git add src/AFK4.Platform.Api/Media src/AFK4.Platform.Api/AFK4.Platform.Api.csproj \
        src/AFK4.Platform.Api/Program.cs src/AFK4.Platform.Api/appsettings.json \
        tests/AFK4.Platform.Api.Tests/Fakes/FakeMediaStorage.cs
git commit -m "feat(media): IMediaStorage + MinIO (AWSSDK.S3) storage + options"
```

---

## Task 3: Сервис — `IMediaService`/`EfMediaService` (валидация + оркестрация)

**Files:**
- Create: `src/AFK4.Platform.Api/Media/IMediaService.cs`
- Create: `src/AFK4.Platform.Api/Media/EfMediaService.cs`
- Create: `src/AFK4.Platform.Api/Media/MediaValidation.cs` (magic-byte sniff)
- Modify: `src/AFK4.Platform.Api/Program.cs` (AddScoped<IMediaService>)
- Test: `tests/AFK4.Platform.Api.Tests/EfMediaServiceTests.cs`

**Interfaces:**
- Consumes: `IMediaStorage`, `PlatformDbContext`, `TimeProvider`, `MediaOptions`.
- Produces:
```csharp
public sealed record MediaServiceResult(bool Succeeded, string? Error, UploadedMediaDto? Media);

public interface IMediaService
{
    // Валидирует, заливает, пишет запись; при purpose с "одиночным" смыслом (branch-logo)
    // удаляет прежний объект того же (branchId, purpose).
    Task<MediaServiceResult> UploadAsync(Guid organizationId, Guid branchId, Guid staffUserId,
        string purpose, string declaredContentType, Stream content, long sizeBytes, CancellationToken ct);
    Task<bool> DeleteAsync(Guid organizationId, Guid branchId, Guid mediaId, CancellationToken ct);
}
```

- [ ] **Step 1: Тесты сервиса (пишем первыми)**

`tests/AFK4.Platform.Api.Tests/EfMediaServiceTests.cs` — создаёт `PlatformDbContext` (InMemory) + `FakeMediaStorage` + `MediaOptions{MaxBytes=10485760}` (через `Options.Create`) + `TimeProvider.System`, конструирует `EfMediaService` напрямую. Кейсы:
```csharp
// PNG magic bytes
static Stream Png() => new MemoryStream(new byte[]{0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A, 0,0,0,0});

[Fact] public async Task Upload_Png_StoresObjectAndRecord() { /* Succeeded, Media.Url != null, storage has 1 object, db has 1 row */ }
[Fact] public async Task Upload_OverMaxBytes_Rejected() { /* sizeBytes=MaxBytes+1 → Succeeded=false, Error про размер, storage пуст */ }
[Fact] public async Task Upload_NonImageMagicBytes_Rejected() { /* stream = "%PDF" → отклонено, storage пуст */ }
[Fact] public async Task Upload_SecondBranchLogo_DeletesPrevious() { /* два upload branch-logo → в db одна запись на (branch,purpose)? или storage удалил старый объект; проверить DeleteAsync вызван / старый объект исчез */ }
[Fact] public async Task Delete_RemovesObjectAndRecord() { }
```
(Полные тела — обычные Arrange/Act/Assert; magic-byte стримы фиксированы как выше; для «не-картинки» использовать `%PDF-1.4` байты.)

- [ ] **Step 2: Запустить — увидеть провал (нет реализации)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EfMediaServiceTests`
Expected: FAIL/не компилируется (`EfMediaService`/`MediaValidation` ещё нет).

- [ ] **Step 3: Magic-byte sniffer**

`src/AFK4.Platform.Api/Media/MediaValidation.cs`:
```csharp
namespace AFK4.Platform.Api.Media;

public static class MediaValidation
{
    // Возвращает канонический content-type по сигнатуре или null, если не разрешённая картинка.
    public static string? SniffImageContentType(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 8 && head[0]==0x89 && head[1]==0x50 && head[2]==0x4E && head[3]==0x47)
            return "image/png";
        if (head.Length >= 3 && head[0]==0xFF && head[1]==0xD8 && head[2]==0xFF)
            return "image/jpeg";
        if (head.Length >= 12 && head[0]==0x52 && head[1]==0x49 && head[2]==0x46 && head[3]==0x46
            && head[8]==0x57 && head[9]==0x45 && head[10]==0x42 && head[11]==0x50)
            return "image/webp";
        return null;
    }

    public static string ExtensionFor(string contentType) => contentType switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/webp" => "webp",
        _ => "bin"
    };
}
```

- [ ] **Step 4: Реализация сервиса**

`src/AFK4.Platform.Api/Media/IMediaService.cs` — интерфейс из блока Interfaces.

`src/AFK4.Platform.Api/Media/EfMediaService.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Media;

public sealed class EfMediaService(
    PlatformDbContext db, IMediaStorage storage, IOptions<MediaOptions> options, TimeProvider clock)
    : IMediaService
{
    public async Task<MediaServiceResult> UploadAsync(Guid organizationId, Guid branchId, Guid staffUserId,
        string purpose, string declaredContentType, Stream content, long sizeBytes, CancellationToken ct)
    {
        if (sizeBytes <= 0 || sizeBytes > options.Value.MaxBytes)
            return new(false, "File exceeds the maximum allowed size.", null);

        // Считать голову для magic-byte и переиграть поток целиком в память (файлы мелкие ≤ MaxBytes).
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        var head = new byte[12];
        var read = await buffer.ReadAsync(head.AsMemory(0, 12), ct);
        var sniffed = MediaValidation.SniffImageContentType(head.AsSpan(0, read));
        if (sniffed is null)
            return new(false, "Unsupported file type. Allowed: PNG, JPEG, WEBP.", null);
        buffer.Position = 0;

        // branch-logo — «одиночный»: удалить прежние объекты этого (branch, purpose).
        var previous = await db.UploadedMedia
            .Where(m => m.OrganizationId == organizationId && m.BranchId == branchId && m.Purpose == purpose)
            .ToListAsync(ct);
        foreach (var old in previous)
        {
            await storage.DeleteAsync(old.ObjectKey, ct);
            db.UploadedMedia.Remove(old);
        }

        var mediaId = Guid.NewGuid();
        var objectKey = $"{organizationId}/{branchId}/{mediaId}.{MediaValidation.ExtensionFor(sniffed)}";
        var url = await storage.PutAsync(objectKey, sniffed, buffer, ct);

        var entity = new UploadedMediaEntity
        {
            MediaId = mediaId, OrganizationId = organizationId, BranchId = branchId,
            Purpose = purpose, ObjectKey = objectKey, ContentType = sniffed,
            SizeBytes = sizeBytes, PublicUrl = url, CreatedByStaffUserId = staffUserId,
            CreatedAtUtc = clock.GetUtcNow()
        };
        db.UploadedMedia.Add(entity);
        await db.SaveChangesAsync(ct);
        return new(true, null, new UploadedMediaDto(mediaId, url, sniffed, sizeBytes));
    }

    public async Task<bool> DeleteAsync(Guid organizationId, Guid branchId, Guid mediaId, CancellationToken ct)
    {
        var entity = await db.UploadedMedia.SingleOrDefaultAsync(
            m => m.MediaId == mediaId && m.OrganizationId == organizationId && m.BranchId == branchId, ct);
        if (entity is null) return false;
        await storage.DeleteAsync(entity.ObjectKey, ct);
        db.UploadedMedia.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
```
DI в `Program.cs`: `builder.Services.AddScoped<IMediaService, EfMediaService>();`

- [ ] **Step 5: Тесты зелёные**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EfMediaServiceTests`
Expected: PASS (все 5 кейсов).

- [ ] **Step 6: Коммит**
```bash
git add src/AFK4.Platform.Api/Media/IMediaService.cs src/AFK4.Platform.Api/Media/EfMediaService.cs \
        src/AFK4.Platform.Api/Media/MediaValidation.cs src/AFK4.Platform.Api/Program.cs \
        tests/AFK4.Platform.Api.Tests/EfMediaServiceTests.cs
git commit -m "feat(media): EfMediaService with magic-byte validation + single-logo replace"
```

---

## Task 4: Endpoint — multipart POST/DELETE, гейт, IDOR, Kestrel-лимит

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/MediaEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (`app.MapMediaEndpoints();` + multipart body-size на группе)
- Test: `tests/AFK4.Platform.Api.Tests/MediaEndpointTests.cs`
- Modify (тест-инфра): `tests/AFK4.Platform.Api.Tests/PlatformApiFactory.cs` (подмена `IMediaStorage` на `FakeMediaStorage`)

**Interfaces:**
- Consumes: `IMediaService`, `StaffAuthorizationService`, `StaffPermissionNames.ManageBranchSettings` (сверить точное имя константы в `StaffPermissionNames` — соответствует `branches.settings.manage`).
- Produces: `POST /api/branches/{branchId}/media` (multipart: `file`, `purpose`) → 200 `UploadedMediaDto`; `DELETE /api/branches/{branchId}/media/{mediaId}` → 204.

> Маппинг purpose→право: для `branch-logo` требуется `ManageBranchSettings`. Пока purpose один, гейтим на `ManageBranchSettings` безусловно; при добавлении purposes ввести map. Если константы `ManageBranchSettings` в `StaffPermissionNames` нет — найти реальное имя права для `/api/branches/{branchId}/settings` (`BranchSettingsEndpoint`) и использовать его.

- [ ] **Step 1: Тесты endpoint (первыми)**

`tests/AFK4.Platform.Api.Tests/MediaEndpointTests.cs` — по образцу `BranchSettingsEndpointTests`:
```csharp
static MultipartFormDataContent PngForm(string purpose = "branch-logo")
{
    var content = new MultipartFormDataContent();
    var bytes = new byte[]{0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0,0,0,0};
    var file = new ByteArrayContent(bytes);
    file.Headers.ContentType = new("application/octet-stream");
    content.Add(file, "file", "logo.png");
    content.Add(new StringContent(purpose), "purpose");
    return content;
}

[Fact] public async Task Post_WithoutToken_Unauthorized() { /* без AuthorizeAs → 401 */ }
[Fact] public async Task Post_WithoutPermission_Forbidden() { /* роль без ManageBranchSettings (напр. Cashier) → 403 */ }
[Fact] public async Task Post_Png_Ok_ReturnsUrlAndPersists() { /* Owner → 200, UploadedMediaDto.Url != null, db has row */ }
[Fact] public async Task Post_NonImage_BadRequest() { /* %PDF байты → 400 */ }
[Fact] public async Task Post_MismatchedBranchOfAnotherOrg_Forbidden() { /* чужой branchId → 403 (IDOR) */ }
[Fact] public async Task Delete_Ok_NoContent() { /* сначала upload, затем DELETE → 204, db пуст */ }
```
Роли: `StaffRoleNames.Owner` (имеет ManageBranchSettings), `StaffRoleNames.Cashier` (не имеет) — сверить по фикстурам ролей; если Cashier не сидит в StaffAuthTestHelper, взять любую роль без права.

- [ ] **Step 2: Подмена IMediaStorage в тест-хосте**

В `tests/AFK4.Platform.Api.Tests/PlatformApiFactory.cs` (`ConfigureWebHost`, где уже заменяется `ISessionBillingService`) добавить:
```csharp
services.RemoveAll<IMediaStorage>();
services.AddScoped<IMediaStorage, FakeMediaStorage>();
```
(`using AFK4.Platform.Api.Media;` + `Microsoft.Extensions.DependencyInjection.Extensions` для `RemoveAll`.)

- [ ] **Step 3: Запустить — провал**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~MediaEndpointTests`
Expected: FAIL (эндпоинта нет).

- [ ] **Step 4: Endpoint**

`src/AFK4.Platform.Api/Endpoints/MediaEndpoints.cs`:
```csharp
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Media;
using AFK4.Shared.Contracts.Media;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AFK4.Platform.Api.Endpoints;

internal static class MediaEndpoints
{
    public static void MapMediaEndpoints(this WebApplication app)
    {
        app.MapPost("/api/branches/{branchId:guid}/media", async (
            Guid branchId,
            [FromForm] string purpose,
            IFormFile file,
            StaffAuthorizationService authorizationService,
            IMediaService mediaService,
            CancellationToken ct) =>
        {
            var auth = await authorizationService.RequireBranchPermissionAsync(
                branchId, StaffPermissionNames.ManageBranchSettings, ct);
            if (!auth.IsAuthenticated) return Results.Unauthorized();
            if (!auth.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (file is null || file.Length == 0) return Results.BadRequest(new { Error = "File is required." });

            await using var stream = file.OpenReadStream();
            var result = await mediaService.UploadAsync(
                auth.StaffContext!.OrganizationId, branchId, auth.StaffContext.StaffUserId,
                purpose, file.ContentType, stream, file.Length, ct);
            return result.Succeeded
                ? Results.Ok(result.Media)
                : Results.BadRequest(new { Error = result.Error });
        }).DisableAntiforgery();

        app.MapDelete("/api/branches/{branchId:guid}/media/{mediaId:guid}", async (
            Guid branchId, Guid mediaId,
            StaffAuthorizationService authorizationService,
            IMediaService mediaService,
            CancellationToken ct) =>
        {
            var auth = await authorizationService.RequireBranchPermissionAsync(
                branchId, StaffPermissionNames.ManageBranchSettings, ct);
            if (!auth.IsAuthenticated) return Results.Unauthorized();
            if (!auth.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var deleted = await mediaService.DeleteAsync(auth.StaffContext!.OrganizationId, branchId, mediaId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }
}
```
В `Program.cs` рядом с прочими `app.Map*Endpoints();`: `app.MapMediaEndpoints();`

- [ ] **Step 5: Kestrel/лимит тела запроса**

Multipart-загрузка должна иметь потолок тела на маршруте. Проще всего — глобально поднять `FormOptions.MultipartBodyLengthLimit` и Kestrel `MaxRequestBodySize` до `MaxBytes` + запас, ЛИБО на эндпоинт-группе. В `Program.cs` (после `builder.Services.Configure<MediaOptions>`):
```csharp
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 12 * 1024 * 1024; // MaxBytes(10) + запас
});
```
(Kestrel дефолт `MaxRequestBodySize` = 30 МБ — покрывает 12 МБ, отдельно поднимать не нужно; если где-то занижен — поднять до 12 МБ. Отметить в отчёте фактический дефолт.)

- [ ] **Step 6: Тесты зелёные**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~MediaEndpointTests`
Expected: PASS (6 кейсов). Затем полный прогон затронутого проекта:
Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: без регрессий.

- [ ] **Step 7: Коммит**
```bash
git add src/AFK4.Platform.Api/Endpoints/MediaEndpoints.cs src/AFK4.Platform.Api/Program.cs \
        tests/AFK4.Platform.Api.Tests/MediaEndpointTests.cs tests/AFK4.Platform.Api.Tests/PlatformApiFactory.cs
git commit -m "feat(media): branch media upload/delete endpoints (multipart, gated, IDOR)"
```

---

## Task 5: Web — переиспользуемый `MediaUpload` компонент + api-client + i18n

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/api/clients/media.ts`
- Create: `src/AFK4.Operator.App.Web/src/components/MediaUpload.tsx`
- Modify: `/locales/{ru,en,tg}.json` (ключи ниже) + `cd packages/i18n && bun run gen`
- Modify: `src/AFK4.Operator.App.Web/src/styles/` (стиль загрузчика — уместный файл, напр. новый блок в `02-ui-kit.css` или `23-management-crud.css`)
- Test: `src/AFK4.Operator.App.Web/src/components/MediaUpload.test.tsx`

**Interfaces:**
- Consumes: паттерн api-client (см. `src/AFK4.Operator.App.Web/src/api/clients/news.ts` / `settings.ts` — как строится клиент поверх `PlatformApiClient`).
- Produces: `MediaUpload` проп-контракт:
```tsx
interface MediaUploadProps {
  value: string | null;                 // текущий URL (или null)
  onChange: (media: { mediaId: string; url: string } | null) => void;
  purpose: string;                      // MediaPurposeNames, напр. 'branch-logo'
  branchId: string;
  backend: OperatorBackendContext;
  disabled?: boolean;
}
```

- [ ] **Step 1: i18n-ключи**

В `/locales/{ru,en,tg}.json` (затем `cd packages/i18n && bun run gen`):
```ts
// ru
"op.media.upload.cta": "Загрузить изображение",
"op.media.upload.replace": "Заменить",
"op.media.upload.remove": "Удалить",
"op.media.upload.hint": "PNG, JPEG или WEBP, до 10 МБ.",
"op.media.upload.uploading": "Загрузка…",
"op.media.upload.errorType": "Только PNG, JPEG или WEBP.",
"op.media.upload.errorSize": "Файл больше 10 МБ.",
// en
"op.media.upload.cta": "Upload image",
"op.media.upload.replace": "Replace",
"op.media.upload.remove": "Remove",
"op.media.upload.hint": "PNG, JPEG or WEBP, up to 10 MB.",
"op.media.upload.uploading": "Uploading…",
"op.media.upload.errorType": "Only PNG, JPEG or WEBP.",
"op.media.upload.errorSize": "File is larger than 10 MB.",
// tg
"op.media.upload.cta": "Боркунии тасвир",
"op.media.upload.replace": "Иваз кардан",
"op.media.upload.remove": "Нест кардан",
"op.media.upload.hint": "PNG, JPEG ё WEBP, то 10 МБ.",
"op.media.upload.uploading": "Боркунӣ…",
"op.media.upload.errorType": "Танҳо PNG, JPEG ё WEBP.",
"op.media.upload.errorSize": "Файл аз 10 МБ калонтар аст.",
```

- [ ] **Step 2: api-client**

`src/AFK4.Operator.App.Web/src/api/clients/media.ts` — по образцу `news.ts`. Метод:
```ts
export interface UploadedMediaDto { mediaId: string; url: string; contentType: string; sizeBytes: number; }

export function createMediaClient(api: PlatformApiClient) {
  return {
    async upload(branchId: string, purpose: string, file: File): Promise<UploadedMediaDto> {
      const form = new FormData();
      form.append('file', file);
      form.append('purpose', purpose);
      // PlatformApiClient должен уметь слать FormData без forced application/json —
      // если нет, добавить метод postForm; сверить с реализацией PlatformApiClient.
      return api.postForm(`/api/branches/${branchId}/media`, form);
    },
    async remove(branchId: string, mediaId: string): Promise<void> {
      await api.delete(`/api/branches/${branchId}/media/${mediaId}`);
    }
  };
}
```
> Проверить `PlatformApiClient`: есть ли отправка `FormData` (без `Content-Type: application/json`). Если нет — добавить `postForm(path, formData)` (fetch с телом FormData, БЕЗ ручного Content-Type, чтобы браузер сам проставил boundary). Токен-заголовок — как в остальных методах.

- [ ] **Step 3: Тест компонента (первым)**

`src/AFK4.Operator.App.Web/src/components/MediaUpload.test.tsx` (`bun test`, happy-dom):
```tsx
// мокнуть media-клиент; проверить:
// 1) без value — видна CTA «Загрузить изображение»;
// 2) выбор слишком большого файла (size > 10MB) → ошибка errorSize, upload НЕ вызван;
// 3) выбор .png в пределах лимита → вызван upload(branchId, purpose, file), onChange получил {mediaId,url};
// 4) при value != null — виден <img src=value> + «Заменить»/«Удалить»; клик «Удалить» → remove + onChange(null).
```
Типизировать bun-моки (build тайпчекает тесты).

- [ ] **Step 4: Запустить — провал**

Run: `cd src/AFK4.Operator.App.Web && bun test src/components/MediaUpload.test.tsx`
Expected: FAIL (компонента нет).

- [ ] **Step 5: Компонент**

`src/AFK4.Operator.App.Web/src/components/MediaUpload.tsx` — реализовать контракт из Interfaces:
- `<input type="file" accept="image/png,image/jpeg,image/webp">` скрытый, кнопка-триггер (тач-таргет ≥44px).
- Клиентская пред-валидация: тип ∈ {png,jpeg,webp} иначе `errorType`; `file.size > 10*1024*1024` иначе `errorSize` — мгновенный feedback до отправки.
- Состояния: idle / uploading (кнопка disabled + текст `uploading`; спиннер можно отложить, но для простоты — текст) / error (конкретный текст) / preview (`<img src=value>` + «Заменить»/«Удалить»).
- Использовать `createMediaClient(createAuthenticatedOperatorClients(...))`-стиль (сверить, как прочие экраны берут клиента; напр. `createAuthenticatedOperatorClients(backend.config, backend.session)`).
- Классы кит-совместимые (`ui-btn`, `ui-btn--sm`); стиль превью-рамки — новый небольшой блок CSS.
- НЕ определять вложенных компонентов внутри тела (урок сессии: ремаунт/потеря фокуса).

- [ ] **Step 6: CSS**

Небольшой блок (напр. в `02-ui-kit.css`): `.media-upload`, `.media-upload-preview img { max-width:100%; border-radius: var(--radius-md); }`, кнопки в ряд, тач-таргеты. Тема-aware через существующие переменные.

- [ ] **Step 7: Тесты + сборка зелёные**

Run: `cd src/AFK4.Operator.App.Web && bun test src/components/MediaUpload.test.tsx`
Expected: PASS.
Run: `cd /home/fedya/projects/afk4.net && bun test packages/i18n/src/messages.test.ts packages/i18n/src/voice.test.ts`
Expected: PASS (паритет + tg≠ru).
Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: успешно.

- [ ] **Step 8: Коммит**
```bash
git add src/AFK4.Operator.App.Web/src/api/clients/media.ts \
        src/AFK4.Operator.App.Web/src/components/MediaUpload.tsx \
        src/AFK4.Operator.App.Web/src/components/MediaUpload.test.tsx \
        src/AFK4.Operator.App.Web/src/styles locales/ru.json locales/en.json locales/tg.json \
        packages/i18n/src/messages.ts
git commit -m "feat(media): reusable MediaUpload component + media api client"
```

---

## Self-Review

**Spec coverage** (против `2026-07-20-operator-media-upload-subsystem-design.md`):
- MinIO server-mediated upload → Task 2 (storage) + Task 4 (endpoint) ✅
- `UploadedMediaEntity` реестр lifecycle → Task 1 + replace/delete в Task 3 ✅
- Валидация размер + magic-byte, только изображения, SVG вне скоупа → Task 3 (`MediaValidation`) ✅
- Public URL, раздача напрямую из MinIO (Platform.Api не раздаёт) → `PublicUrlFor`, Task 2 ✅
- AWSSDK.S3 (решение принято на ревью) → Task 2 ✅
- Endpoint гейт `ManageBranchSettings` + IDOR → Task 4 ✅
- Конфиг env, секреты не в коде → Task 2 (MediaOptions + appsettings пустой) ✅
- UI `MediaUpload` переиспользуемый, состояния, тач ≥44px, i18n ru/en/tg → Task 5 ✅
- Тесты backend (unauth/forbidden/success/IDOR/размер/тип/delete) + фронт → Tasks 3/4/5 ✅
- Deploy-пререквизиты (миграция на staging, провижн бакета) → Global Constraints (флаг, не код) ✅
- Вне скоупа (presigned, ресайз, News-миграция, SVG) — не реализуется ✅

**Placeholder scan:** кода-заглушек нет; тела тестов Task 3/5 описаны кейсами с фиксированными magic-byte-стримами (не «напиши тесты»); реальные сигнатуры везде.

**Type consistency:** `IMediaStorage` (Put/Delete/PublicUrlFor) — Task 2 определяет, Task 3 потребляет, Task 4/фейк реализует одинаково. `MediaServiceResult`/`IMediaService.UploadAsync` сигнатура едина в Task 3 и вызове Task 4. `UploadedMediaDto` (MediaId/Url/ContentType/SizeBytes) — контракт Task 1, возвращается Task 3, типизирован в клиенте Task 5.

**Риск-заметки исполнителю:**
- Сверить точное имя `StaffPermissionNames.ManageBranchSettings` (Task 4) — если иное, взять право эндпоинта `/api/branches/{branchId}/settings`.
- Сверить, умеет ли `PlatformApiClient` слать `FormData` (Task 5 Step 2) — если нет, добавить `postForm`/`delete` аккуратно, не ломая JSON-путь.
- `IFormFile`+`[FromForm]` в minimal API требует `.DisableAntiforgery()` (уже в коде) и корректного multipart-биндинга; если версия ASP.NET капризит на `[FromForm] string` рядом с `IFormFile` — принять оба через `HttpRequest.ReadFormAsync()` и достать `Request.Form["purpose"]` + `Request.Form.Files["file"]`.
