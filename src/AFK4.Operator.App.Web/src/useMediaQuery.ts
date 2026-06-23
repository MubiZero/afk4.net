import { useEffect, useState } from 'react';

// Подписка на CSS media query из React. Источник истины для раскладок, которые
// должны и менять DOM (например 2↔3 колонки), и переключать загрузку данных —
// чистым CSS такое не выразить. Без matchMedia (SSR/тестовый happy-dom) → false,
// то есть «узкий» дефолт: безопаснее не показывать тяжёлую широкую раскладку.
export function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return false;
    }
    return window.matchMedia(query).matches;
  });

  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return undefined;
    }

    const mql = window.matchMedia(query);
    const onChange = () => setMatches(mql.matches);
    onChange();
    mql.addEventListener('change', onChange);
    return () => mql.removeEventListener('change', onChange);
  }, [query]);

  return matches;
}
