import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { PlanFormDialog } from './PlanFormDialog';
import { emptyPlanForm } from './billingModel';

function renderDialog(over: Partial<Parameters<typeof PlanFormDialog>[0]> = {}) {
  return render(
    <I18nProvider>
      <PlanFormDialog open mode="create" form={emptyPlanForm()} pending={false} onChange={vi.fn()} onSubmit={vi.fn()} onOpenChange={vi.fn()} {...over} />
    </I18nProvider>
  );
}

describe('PlanFormDialog', () => {
  it('renders the create title', () => {
    renderDialog();
    expect(screen.getByText('Новый тариф')).toBeInTheDocument();
  });
});
