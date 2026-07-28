import type { ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';

export function ScanSearchBar({
  icon,
  value,
  onChange,
  placeholder,
  ariaLabel,
  trailing,
}: {
  icon: ReactNode;
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  ariaLabel: string;
  trailing?: ReactNode;
}) {
  const { t } = useI18n();
  return (
    <div className="stock-scanbar">
      <label className="ui-search-field stock-scanbar-search">
        {icon}
        <input
          type="search"
          aria-label={ariaLabel}
          placeholder={placeholder}
          value={value}
          onChange={(event) => onChange(event.currentTarget.value)}
        />
      </label>
      <span className="ui-scanner-badge" aria-label={t('op.pos.scan.active')}>
        <span className="ui-scanner-pulse" aria-hidden="true" />
        {t('op.pos.scan.active')}
      </span>
      {trailing}
    </div>
  );
}
