import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import { ThemeToggle } from './ThemeToggle';
import { LanguageToggle } from './LanguageToggle';

export interface UserMenuProps { displayName: string; roleLabel: string; onSignOut: () => void; }

export function UserMenu({ displayName, roleLabel, onSignOut }: UserMenuProps) {
  const { t } = useI18n();
  return (
    <div className="mt-auto flex items-center gap-3 border-t border-border px-3 py-3">
      <Avatar className="size-8"><AvatarFallback>{displayName.slice(0, 1)}</AvatarFallback></Avatar>
      <div className="min-w-0">
        <div className="truncate text-sm font-semibold">{displayName}</div>
        <div className="truncate text-[11px] text-muted">{roleLabel}</div>
      </div>
      <div className="ml-auto flex items-center gap-1">
        <LanguageToggle />
        <ThemeToggle />
        <Button variant="ghost" size="sm" onClick={onSignOut}>{t('shell.signOut')}</Button>
      </div>
    </div>
  );
}
