export type PlayerRoute =
  | { kind: 'dashboard' }
  | { kind: 'history' }
  | { kind: 'receipt'; sessionId: string }
  | { kind: 'purchases' }
  | { kind: 'reservations' }
  | { kind: 'profile' };

export type PlayerTab = 'dashboard' | 'history' | 'reservations' | 'profile';

export function resolvePlayerRoute(pathname: string): PlayerRoute {
  const parts = pathname.split('/').filter(Boolean);
  if (parts.length === 0) return { kind: 'dashboard' };
  if (parts[0] === 'history' && parts[2] === 'receipt') return { kind: 'receipt', sessionId: parts[1] };
  if (parts[0] === 'history') return { kind: 'history' };
  if (parts[0] === 'purchases') return { kind: 'purchases' };
  if (parts[0] === 'reservations') return { kind: 'reservations' };
  if (parts[0] === 'profile') return { kind: 'profile' };
  return { kind: 'dashboard' };
}

export function routePath(route: PlayerRoute): string {
  switch (route.kind) {
    case 'dashboard': return '/';
    case 'history': return '/history';
    case 'receipt': return `/history/${route.sessionId}/receipt`;
    case 'purchases': return '/purchases';
    case 'reservations': return '/reservations';
    case 'profile': return '/profile';
  }
}
