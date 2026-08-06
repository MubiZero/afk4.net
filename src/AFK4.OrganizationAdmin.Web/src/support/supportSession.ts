const KEY = 'afk4.support.session';

export interface SupportSession {
  sessionToken: string;
  organizationId: string;
  organizationName: string;
  reason: string;
  expiresAtUtc: string;
  writableAreas: string[];
}

export function readSupportSession(): SupportSession | null {
  const raw = sessionStorage.getItem(KEY);
  if (!raw) return null;
  try {
    const s = JSON.parse(raw) as SupportSession;
    if (!s.sessionToken || !s.organizationId) return null;
    return s;
  } catch {
    return null;
  }
}

export function writeSupportSession(session: SupportSession): void {
  sessionStorage.setItem(KEY, JSON.stringify(session));
}

export function clearSupportSession(): void {
  sessionStorage.removeItem(KEY);
}

// Публичный обмен: у клиента ещё нет ни staff-токена, ни сессии поддержки, поэтому это простой
// fetch без PlatformApiClient (который требует токен на каждый вызов). Билет — секрет одноразового
// действия: не логируем его и не кладём в сообщение об ошибке.
export async function redeemSupportTicket(baseUrl: string, ticket: string): Promise<SupportSession> {
  const url = new URL('/api/public/support-access/sessions', baseUrl).toString();
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ticket })
  });

  if (!response.ok) {
    throw new Error('Support ticket is invalid, already used, or expired.');
  }

  return await response.json() as SupportSession;
}
