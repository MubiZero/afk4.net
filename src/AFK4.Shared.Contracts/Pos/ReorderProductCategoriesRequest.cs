namespace AFK4.Shared.Contracts.Pos;

/// <summary>
/// Новый порядок категорий филиала: весь список целиком, сверху вниз.
///
/// Список, а не пара «категория + номер»: порядок — свойство набора, и присланный целиком он не
/// оставляет места расхождению. Пара «id + номер» на каждое перетаскивание порождала бы дыры и
/// совпадения в нумерации, которые потом нечем разрешить.
/// </summary>
public sealed record ReorderProductCategoriesRequest(
    Guid OrganizationId,
    IReadOnlyList<Guid> CategoryIds);
