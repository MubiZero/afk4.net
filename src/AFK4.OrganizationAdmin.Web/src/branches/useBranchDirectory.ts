import { useEffect, useState } from 'react';

export interface BranchDirectoryEntry {
  name: string;
}

/**
 * Дозагружает имена филиалов параллельно по списку id (профиль филиала — отдельный запрос,
 * общего batch-эндпоинта нет). `getProfile` внедряется снаружи, чтобы хук не был завязан на
 * конкретный API-клиент и легко тестировался без сети. Сбой отдельного запроса не валит весь
 * справочник — фолбэк для такой записи = сам branchId (лучше показать id, чем спрятать филиал).
 */
export function useBranchDirectory(
  getProfile: (branchId: string) => Promise<BranchDirectoryEntry>,
  branchIds: readonly string[]
): Record<string, BranchDirectoryEntry> {
  const [directory, setDirectory] = useState<Record<string, BranchDirectoryEntry>>({});

  useEffect(() => {
    let alive = true;
    Promise.all(
      branchIds.map(async (branchId) => {
        try {
          const profile = await getProfile(branchId);
          return [branchId, { name: profile.name }] as const;
        } catch {
          return [branchId, { name: branchId }] as const;
        }
      })
    ).then((entries) => {
      if (alive) setDirectory(Object.fromEntries(entries));
    });
    return () => {
      alive = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branchIds.join(','), getProfile]);

  return directory;
}
