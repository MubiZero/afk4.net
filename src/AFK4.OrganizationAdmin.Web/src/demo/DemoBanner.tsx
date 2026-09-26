import { useI18n } from '@afk4/i18n';

/**
 * Публичное демо Панели: данные примерные, ничего не уходит на сервер, и каждая загрузка страницы
 * начинает с чистого листа. Полоса стоит в том же несъёмном слоте, что плашка долга и режим
 * поддержки, — посетитель не должен принять пример за чужой настоящий клуб.
 */
export function DemoBanner({ reload = () => window.location.reload() }: { reload?: () => void }) {
  const { t } = useI18n();
  const restart = () => {
    try {
      sessionStorage.clear();
      localStorage.clear();
    } catch {
      // Хранилище закрыто — достаточно перезагрузки: данные демо живут в памяти страницы.
    }
    reload();
  };

  return (
    <div className="billing-status-banner demo-banner" role="status">
      <span className="billing-status-banner-badge">{t('op.demo.badge')}</span>
      <span className="billing-status-banner-message">{t('op.demo.text')}</span>
      <button type="button" className="demo-banner-restart" onClick={restart}>{t('op.demo.restart')}</button>
    </div>
  );
}
