import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ConnectionResolutionScreen } from './ConnectionResolutionScreen';

function setup() {
  const resolver = {
    resolveBySlugPair: mock().mockResolvedValue({ organizationStatus: 'active' }),
    resolveBySetupCode: mock().mockResolvedValue({ organizationStatus: 'active' })
  };
  const onResolved = mock();
  render(
    <I18nProvider>
      <ConnectionResolutionScreen resolver={resolver as never} onResolved={onResolved} />
    </I18nProvider>
  );
  return { resolver, onResolved };
}

it('renders the localized connection screen (ru default)', () => {
  setup();
  expect(screen.getByRole('heading', { name: 'Подключение клуба' })).toBeInTheDocument();
  expect(screen.getByText('Ключ клуба')).toBeInTheDocument();
});

it('submits the slug pair to the resolver', async () => {
  const { resolver, onResolved } = setup();
  fireEvent.change(screen.getByLabelText('Ключ клуба'), { target: { value: 'acme' } });
  fireEvent.change(screen.getByLabelText('Ключ филиала'), { target: { value: 'main' } });
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }));
  await waitFor(() => expect(resolver.resolveBySlugPair).toHaveBeenCalledWith('acme', 'main'));
  await waitFor(() => expect(onResolved).toHaveBeenCalled());
});

it('switches to the setup-code mode', () => {
  setup();
  fireEvent.click(screen.getByRole('button', { name: 'Код подключения' }));
  expect(screen.getByLabelText('Код подключения')).toBeInTheDocument();
});
