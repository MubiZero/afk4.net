import { describe, expect, it, mock } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { PlanFormDialog } from './PlanFormDialog';
import { emptyPlanForm } from './billingModel';

function renderDialog(over: Partial<Parameters<typeof PlanFormDialog>[0]> = {}) {
  return render(
    <I18nProvider>
      <PlanFormDialog open mode="create" form={emptyPlanForm()} pending={false} onChange={mock()} onSubmit={mock()} onOpenChange={mock()} {...over} />
    </I18nProvider>
  );
}

describe('PlanFormDialog', () => {
  it('renders the create title', () => {
    renderDialog();
    expect(screen.getByText('Новый тариф')).toBeInTheDocument();
  });

  // Код — ключ, по которому тариф записан у организаций. В правке поле серое, и без слов
  // это читалось как сбой формы.
  it('в правке объясняет, почему код тарифа не меняется', () => {
    renderDialog({ mode: 'edit', form: { ...emptyPlanForm(), planCode: 'growth', name: 'Growth' } });

    const code = screen.getByLabelText('Код тарифа');
    expect(code).toBeDisabled();
    expect(code.getAttribute('aria-describedby')).toBe(screen.getByText('Код тарифа после создания не меняется: по нему тариф записан у организаций.').id);
  });

  it('у нового тарифа код открыт и ничего не объясняет', () => {
    renderDialog();

    const code = screen.getByLabelText('Код тарифа');
    expect(code).toBeEnabled();
    expect(code.getAttribute('aria-describedby')).toBeNull();
    expect(screen.queryByText(/Код тарифа после создания не меняется/)).toBeNull();
  });
});
