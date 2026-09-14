namespace AFK4.Shared.Contracts.Pos;

// Переименование категории. Ключа идемпотентности здесь нет намеренно: повтор с тем же именем
// приводит к тому же состоянию, а денег операция не двигает — в отличие от создания, где повтор
// завёл бы вторую категорию.
public sealed record RenameProductCategoryRequest(Guid OrganizationId, string Name);
