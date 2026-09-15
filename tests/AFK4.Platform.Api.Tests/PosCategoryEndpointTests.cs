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
            new UpdateProductCategoryRequest(TestIds.OrganizationId, "Снэки"));

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
            new UpdateProductCategoryRequest(TestIds.OrganizationId, "напитки"));

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
            new UpdateProductCategoryRequest(TestIds.OrganizationId, "Снеки"));

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
            new UpdateProductCategoryRequest(TestIds.OrganizationId, "Снеки"));

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
            new UpdateProductCategoryRequest(TestIds.OrganizationId, "Снэки"));

        Assert.Equal(HttpStatusCode.Forbidden, renamed.StatusCode);
    }

    // Удаления нет нарочно: что делать с товарами удаляемой категории — решение владельца, а не
    // кнопки. Скрытие убирает категорию со стойки, не трогая ни товары, ни историю чеков.
    [Fact]
    public async Task HiddenCategory_StaysInTheDirectoryWithItsName()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var created = await CreateAsync(client, "Снеки", "category-hide-1");
        var category = await created.Content.ReadFromJsonAsync<PosProductCategoryDto>();

        var hidden = await client.PatchAsJsonAsync(
            $"{CategoriesPath}/{category!.CategoryId:D}",
            new UpdateProductCategoryRequest(TestIds.OrganizationId, IsActive: false));

        Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        var categories = await client.GetFromJsonAsync<PosProductCategoryDto[]>(CategoriesPath);
        var listed = Assert.Single(categories!);
        Assert.False(listed.IsActive);
        Assert.Equal("Снеки", listed.Name);
    }

    // Запрос без имени не должен затирать название пустотой — и наоборот.
    [Fact]
    public async Task RenameDoesNotChangeVisibility_AndHidingDoesNotChangeTheName()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var created = await CreateAsync(client, "Снеки", "category-partial-1");
        var category = await created.Content.ReadFromJsonAsync<PosProductCategoryDto>();
        var path = $"{CategoriesPath}/{category!.CategoryId:D}";

        await client.PatchAsJsonAsync(path, new UpdateProductCategoryRequest(TestIds.OrganizationId, IsActive: false));
        var renamed = await client.PatchAsJsonAsync(path, new UpdateProductCategoryRequest(TestIds.OrganizationId, "Снэки"));

        var updated = await renamed.Content.ReadFromJsonAsync<PosProductCategoryDto>();
        Assert.Equal("Снэки", updated!.Name);
        Assert.False(updated.IsActive);
    }

    // Пустая правка — не «готово»: ответить успехом значит подтвердить изменение, которого не было.
    [Fact]
    public async Task UpdateWithNeitherNameNorVisibility_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var created = await CreateAsync(client, "Снеки", "category-empty-1");
        var category = await created.Content.ReadFromJsonAsync<PosProductCategoryDto>();

        var response = await client.PatchAsJsonAsync(
            $"{CategoriesPath}/{category!.CategoryId:D}",
            new UpdateProductCategoryRequest(TestIds.OrganizationId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Новая категория встаёт в конец ручной расстановки, а не перед ней.
    [Fact]
    public async Task ReorderedCategories_KeepTheSentOrder_AndANewOneGoesLast()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var batteries = (await (await CreateAsync(client, "Батарейки", "order-1")).Content
            .ReadFromJsonAsync<PosProductCategoryDto>())!;
        var drinks = (await (await CreateAsync(client, "Напитки", "order-2")).Content
            .ReadFromJsonAsync<PosProductCategoryDto>())!;

        var reordered = await client.PostAsJsonAsync(
            $"{CategoriesPath}/order",
            new ReorderProductCategoriesRequest(
                TestIds.OrganizationId,
                new[] { drinks.CategoryId, batteries.CategoryId }));
        Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);
        await CreateAsync(client, "Снеки", "order-3");

        var categories = await client.GetFromJsonAsync<PosProductCategoryDto[]>(CategoriesPath);

        Assert.Equal(
            new[] { "Напитки", "Батарейки", "Снеки" },
            categories!.Select(category => category.Name).ToArray());
    }

    // Частичный список значит, что кто-то завёл категорию, пока экран был открыт. Расставить по
    // устаревшему списку — тихо уронить чужую работу в конец.
    [Fact]
    public async Task ReorderWithAnIncompleteList_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var first = (await (await CreateAsync(client, "Батарейки", "partial-1")).Content
            .ReadFromJsonAsync<PosProductCategoryDto>())!;
        await CreateAsync(client, "Напитки", "partial-2");

        var response = await client.PostAsJsonAsync(
            $"{CategoriesPath}/order",
            new ReorderProductCategoriesRequest(TestIds.OrganizationId, new[] { first.CategoryId }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReorderWithoutCatalogPermission_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Accountant);

        var response = await client.PostAsJsonAsync(
            $"{CategoriesPath}/order",
            new ReorderProductCategoriesRequest(TestIds.OrganizationId, new[] { Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
