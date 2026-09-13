namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Эндпоинт организационного домена, которым пользуется не Organization Admin. Проверка версии
/// панели к таким не применяется: мастер установки — отдельный продукт со своим циклом обновления
/// (MSI), и требовать от него заголовки чужого приложения нельзя.
/// </summary>
public sealed record NonOrganizationAdminClientMetadata;
