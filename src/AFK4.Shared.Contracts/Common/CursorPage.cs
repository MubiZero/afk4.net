using System.Collections.Generic;

namespace AFK4.Shared.Contracts.Common;

// A page of results plus the cursor to fetch the next page (null when exhausted).
//
// Курсор — это «продолжить отсюда», а не номер страницы: список растёт с одного конца, и смещение
// съезжало бы на каждой новой записи.
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
