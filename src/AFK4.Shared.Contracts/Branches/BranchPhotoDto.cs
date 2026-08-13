namespace AFK4.Shared.Contracts.Branches;

/// <summary>Одно фото зала. MediaId нужен, чтобы удалить объект из хранилища вместе со
/// строкой галереи; у фото, добавленного ссылкой, его нет.</summary>
public sealed record BranchPhotoDto(string Url, Guid? MediaId);
