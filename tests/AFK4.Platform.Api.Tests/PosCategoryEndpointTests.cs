using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Pos;

namespace AFK4.Platform.Api.Tests;

// Категории умели ровно одно — заводиться. Ни списка, ни переименования не существовало ни на
// сервере, ни в интерфейсе: опечатка в названии, сделанная при заведении первого товара,
// оставалась в меню бара навсегда.
public sealed class PosCategoryEndpointTests
{
    private static string CategoriesPath => $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/pos/categories";

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string name, string idempotencyKey) =>
        client.PostAsJsonAsync(CategoriesPath, new CreateProductCategoryRequest(TestIds.OrganizationId, name, idempotencyKey));

    // Имя хранилось приведённым к верхнему регистру: нормализация, нужная для сравнения «такая уже
    // есть», записывалась в отображаемое поле. Оператор вводил «Снеки», в меню появлялось «СНЕКИ».
    [Fact]
    public async Task CreatedCategory_KeepsTheNameAsTyped()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var created = await CreateAsync(client, "Снеки", "category-case-1");
        var category = await created.Content.ReadFromJsonAsync<PosProductCategoryDto>();

        Assert.Equal("Снеки", category!.Name);
    }

    [Fact]
    public async Task ListedCategory_IncludesOneWithNoProducts()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        await CreateAsync(client, "Снеки", "category-list-1");

        var categories = await client.GetFromJsonAsync<PosProductCategoryDto[]>(CategoriesPath);

        var category = Assert.Single(categories!);
        Assert.Equal("Снеки", category.Name);
    }

    [Fact]
    public async Task RenamedCategory_KeepsItsIdAndShowsTheNewName()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var created = await CreateAsync(client, "Снеки", "category-rename-1");
        var category = await created.Content.ReadFromJsonAsync<PosProductCategoryDto>();

        var renamed = await client.PatchAsJsonAsync(
            $"{CategoriesPath}/{category!.CategoryId:D}",
            new RenameProductCategoryRequest(TestIds.OrganizationId, "Снэки"));

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var updated = await renamed.Content.ReadFromJsonAsync<PosProductCategoryDto>();
        Assert.Equal(category.CategoryId, updated!.CategoryId);
        Assert.Equal("Снэки", updated.Name);
        var categories = await client.GetFromJsonAsync<PosProductCategoryDto[]>(CategoriesPath);
        Assert.Equal("Снэки", Assert.Single(categories!).Name);
    }

    // Две категории с одним именем неразличимы в выборе товара — оператор попадёт не в ту.
    [Fact]
    public async Task RenameToAnExistingName_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        await CreateAsync(client, "Напитки", "category-dup-1");
        var second = await CreateAsync(client, "Снеки", "category-dup-2");
        var category = await second.Content.ReadFromJsonAsync<PosProductCategoryDto>();

        var renamed = await client.PatchAsJsonAsync(
            $"{CategoriesPath}/{category!.CategoryId:D}",
            new RenameProductCategoryRequest(TestIds.OrganizationId, "напитки"));

        Assert.Equal(HttpStatusCode.BadRequest, renamed.StatusCode);
    }

    // Поправить регистр собственного имени — не столкновение с самим собой.
    [Fact]
    public async Task RenameToADifferentCaseOfItsOwnName_IsAllowed()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var created = await CreateAsync(client, "снеки", "category-self-1");
        var category = await created.Content.ReadFromJsonAsync<PosProductCategoryDto>();

        var renamed = await client.PatchAsJsonAsync(
            $"{CategoriesPath}/{category!.CategoryId:D}",
            new RenameProductCategoryRequest(TestIds.OrganizationId, "Снеки"));

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Снеки", (await renamed.Content.ReadFromJsonAsync<PosProductCategoryDto>())!.Name);
    }

    [Fact]
    public async Task RenameUnknownCategory_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var renamed = await client.PatchAsJsonAsync(
            $"{CategoriesPath}/{Guid.NewGuid():D}",
            new RenameProductCategoryRequest(TestIds.OrganizationId, "Снеки"));

        Assert.Equal(HttpStatusCode.NotFound, renamed.StatusCode);
    }

    // Каталогом распоряжается не каждый, кто его видит. Права проверяются раньше существования
    // категории — поэтому отказ приходит и на несуществующую: чужой отказ не должен подсказывать,
    // какие идентификаторы в филиале настоящие.
    [Fact]
    public async Task RenameWithoutCatalogPermission_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Accountant);

        var renamed = await client.PatchAsJsonAsync(
            $"{CategoriesPath}/{Guid.NewGuid():D}",
            new RenameProductCategoryRequest(TestIds.OrganizationId, "Снэки"));

        Assert.Equal(HttpStatusCode.Forbidden, renamed.StatusCode);
    }

    // Читать справочник может каждый, кто читает склад: бухгалтеру категории видны, распоряжаться
    // каталогом — нет. Разные права, и список идёт за правом чтения, а не за правом правки.
    [Fact]
    public async Task ListFollowsTheInventoryReadPermission()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Accountant);

        var response = await client.GetAsync(CategoriesPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
