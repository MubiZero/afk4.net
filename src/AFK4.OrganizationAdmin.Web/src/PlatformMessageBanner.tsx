import { useCallback, useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Megaphone } from 'lucide-react';
import { PanelModal } from './PanelModal';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import type { PlatformMessageDto } from './operatorApiClients';
import type { OperatorBackendContext } from './operatorTypes';

export interface PlatformMessagesClient {
  list(): Promise<PlatformMessageDto[]>;
  markRead(announcementId: string): Promise<void>;
}

export interface PlatformMessagesState {
  messages: PlatformMessageDto[];
  unread: PlatformMessageDto[];
  pendingId: string | null;
  markRead: (announcementId: string) => Promise<void>;
}

/**
 * Сообщения платформы клубу. Не путать с «Новостями» (`NewsWorkspace`) — те идут в обратную
 * сторону, от клуба его игрокам.
 *
 * Отметка «прочитано» относится к этому сотруднику: у коллег полоса останется, пока они сами её
 * не закроют. Порядок важности задаёт сервер, здесь он только сохраняется.
 */
export function usePlatformMessages(
  backend: OperatorBackendContext | null,
  injectedClient?: PlatformMessagesClient
): PlatformMessagesState {
  const [messages, setMessages] = useState<PlatformMessageDto[]>([]);
  const [pendingId, setPendingId] = useState<string | null>(null);

  const resolveClient = useCallback((): PlatformMessagesClient | null => {
    if (injectedClient !== undefined) return injectedClient;
    if (backend === null) return null;
    return createAuthenticatedOperatorClients(backend.config, backend.session).platformMessages;
  }, [injectedClient, backend]);

  useEffect(() => {
    let cancelled = false;
    const client = resolveClient();
    if (client === null) return;
    client.list()
      // Проверка формы стоит и здесь, а не только в клиенте: хук принимает любой клиент, а
      // `undefined` дальше по коду доезжает до `.filter` и роняет весь шелл в белое —
      // ErrorBoundary над оболочкой нет.
      .then(loaded => { if (!cancelled) setMessages(Array.isArray(loaded) ? loaded : []); })
      // Сообщение платформы — не то, ради чего показывают ошибку поверх смены: не пришло, значит
      // полосы просто нет. Так же ведут себя выключенные фичи.
      .catch(() => { if (!cancelled) setMessages([]); });
    return () => { cancelled = true; };
  }, [resolveClient]);

  const markRead = useCallback(async (announcementId: string) => {
    const client = resolveClient();
    if (client === null) return;
    setPendingId(announcementId);
    try {
      await client.markRead(announcementId);
      setMessages(previous => previous.map(message =>
        message.announcementId === announcementId ? { ...message, isRead: true } : message));
    } catch {
      // Отметка не прошла — полоса остаётся. Спрятать её локально значило бы соврать: после
      // перезагрузки сообщение вернулось бы непрочитанным.
    } finally {
      setPendingId(null);
    }
  }, [resolveClient]);

  return { messages, unread: messages.filter(message => !message.isRead), pendingId, markRead };
}

/**
 * Полоса показывает ОДНО сообщение — самое важное из непрочитанных. Три полосы подряд не читает
 * никто, а четвёртую закрывают не глядя.
 */
export function PlatformMessageBanner({
  state,
  onOpenHistory
}: {
  state: PlatformMessagesState;
  onOpenHistory: () => void;
}) {
  const { t } = useI18n();
  const current = state.unread[0] ?? null;
  if (current === null) return null;

  return (
    <div className={`platform-message platform-message-${current.severity}`} role="status">
      <Megaphone size={16} aria-hidden />
      <span className="platform-message-title">{current.title}</span>
      <span className="platform-message-body">{current.body}</span>
      <button type="button" className="platform-message-link" onClick={onOpenHistory}>
        {t('platform.messages.openAll')}
      </button>
      <button
        type="button"
        className="platform-message-link"
        disabled={state.pendingId === current.announcementId}
        onClick={() => void state.markRead(current.announcementId)}
      >
        {t('platform.messages.markRead')}
      </button>
    </div>
  );
}

/**
 * Постоянный вход в историю. Без него сообщения были бы доступны только через полосу — а она
 * уступает слот режиму поддержки и плашке задолженности и исчезает, как только всё прочитано.
 */
export function PlatformMessagesButton({
  state,
  onOpen
}: {
  state: PlatformMessagesState;
  onOpen: () => void;
}) {
  const { t } = useI18n();
  if (state.messages.length === 0) return null;

  return (
    <button type="button" className="command-messages" aria-label={t('platform.messages.title')} onClick={onOpen}>
      <Megaphone size={16} />
      {state.unread.length > 0 ? <span className="command-messages-count">{state.unread.length}</span> : null}
    </button>
  );
}

export function PlatformMessagesHistory({
  state,
  onClose
}: {
  state: PlatformMessagesState;
  onClose: () => void;
}) {
  const { t } = useI18n();

  return (
    <PanelModal
      title={t('platform.messages.title')}
      subtitle={t('platform.messages.description')}
      onClose={onClose}
    >
      {state.messages.length === 0 ? (
        <p className="mgmt-drawer-hint">{t('platform.messages.empty')}</p>
      ) : (
        <ul className="platform-message-list">
          {state.messages.map(message => (
            <li
              key={message.announcementId}
              className={message.isRead ? 'platform-message-item read' : 'platform-message-item'}
            >
              <strong>{message.title}</strong>
              <p>{message.body}</p>
              {message.isRead ? (
                <span className="platform-message-badge">{t('platform.messages.read')}</span>
              ) : (
                <button
                  type="button"
                  disabled={state.pendingId === message.announcementId}
                  onClick={() => void state.markRead(message.announcementId)}
                >
                  {t('platform.messages.markRead')}
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
    </PanelModal>
  );
}
