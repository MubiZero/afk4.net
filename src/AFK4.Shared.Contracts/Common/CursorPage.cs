using System.Collections.Generic;

namespace AFK4.Shared.Contracts.Common;

// A page of results plus the cursor to fetch the next page (null when exhausted).
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
