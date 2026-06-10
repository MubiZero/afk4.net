import { useEffect, useMemo, useState } from 'react';
import type { ShellApi } from '../shellApi';
import { OfflineError } from '../shellApi';
import type { PlayerNewsItemDto } from '../apiTypes';
import { createCachedLoader, indexedDbStore } from '../idbCache';

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('ru-RU');
}

export function NewsScreen({ api, onDone }: { api: ShellApi; onDone: () => void }) {
  const [items, setItems] = useState<PlayerNewsItemDto[] | null>(null);
  const [offline, setOffline] = useState(false);
  const load = useMemo(
    () => createCachedLoader<PlayerNewsItemDto[]>(indexedDbStore(), 'news', () => api.getNews()),
    [api]
  );

  useEffect(() => {
    let active = true;
    load().then(
      (data) => { if (active) setItems(data); },
      (error) => {
        if (!active) return;
        if (error instanceof OfflineError) setOffline(true);
        setItems([]);
      }
    );
    return () => { active = false; };
  }, [load]);

  if (items === null) {
    return <section><h2>Новости</h2><p>Загрузка…</p></section>;
  }

  return (
    <section>
      <h2>Новости</h2>
      {offline && <p>Нет связи — показаны последние сохранённые новости.</p>}
      {items.length === 0 && <p>Новостей пока нет.</p>}
      <ul>
        {items.map((item) => (
          <li key={item.id}>
            {item.imageUrl && (
              <img
                src={item.imageUrl}
                alt=""
                onError={(event) => { event.currentTarget.style.display = 'none'; }}
              />
            )}
            <h3>{item.title}</h3>
            <p>{item.body}</p>
            <small>{formatDate(item.publishedAtUtc)}</small>
          </li>
        ))}
      </ul>
      <button type="button" onClick={onDone}>Назад</button>
    </section>
  );
}
