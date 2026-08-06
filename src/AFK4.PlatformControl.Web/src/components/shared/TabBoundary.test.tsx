import { render, screen, fireEvent } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, spyOn } from 'bun:test';
import type { Mock } from 'bun:test';
import { TabBoundary } from './TabBoundary';

// React logs caught render errors to console.error even when an error boundary
// handles them. Silence that expected noise for these tests only.
let consoleError: Mock<typeof console.error>;
beforeEach(() => { consoleError = spyOn(console, 'error').mockImplementation(() => {}); });
afterEach(() => { consoleError.mockRestore(); });

function Thrower(): null {
  throw new Error('boom');
}

function FlagThrower({ flag }: { flag: { throwError: boolean } }) {
  if (flag.throwError) throw new Error('boom');
  return <div>Recovered</div>;
}

it('catches a render throw, shows the fallback, and leaves the passport and sibling tabs alive', () => {
  render(
    <div>
      <div>Passport content</div>
      <TabBoundary message="Section failed" retryLabel="Retry" resetKey="k"><Thrower /></TabBoundary>
      <TabBoundary message="Section failed" retryLabel="Retry" resetKey="k"><div>Sibling tab content</div></TabBoundary>
    </div>
  );

  // (1) the passport (outside any boundary) is unaffected.
  expect(screen.getByText('Passport content')).toBeVisible();
  // (2) the sibling tab, in its own boundary, rendered normally.
  expect(screen.getByText('Sibling tab content')).toBeVisible();
  // (3) the crashing tab shows the error state instead of taking the page down.
  expect(screen.getByRole('alert')).toHaveTextContent('Section failed');
});

it('retries rendering the same children on demand', () => {
  const flag = { throwError: true };
  render(<TabBoundary message="Section failed" retryLabel="Retry" resetKey="k"><FlagThrower flag={flag} /></TabBoundary>);

  expect(screen.getByRole('alert')).toBeVisible();

  flag.throwError = false;
  fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

  expect(screen.getByText('Recovered')).toBeVisible();
  expect(screen.queryByRole('alert')).not.toBeInTheDocument();
});

it('does not clear the failed state just because the children element identity changes', () => {
  const { rerender } = render(
    <TabBoundary message="Section failed" retryLabel="Retry" resetKey="org-1:clubs"><Thrower /></TabBoundary>
  );
  expect(screen.getByRole('alert')).toBeVisible();

  // Same resetKey, brand-new JSX identity (as happens on almost every parent
  // re-render) — the boundary must keep showing the fallback rather than
  // silently retrying (and either flashing recovered content or looping).
  rerender(
    <TabBoundary message="Section failed" retryLabel="Retry" resetKey="org-1:clubs"><div>New content</div></TabBoundary>
  );
  expect(screen.getByRole('alert')).toBeVisible();
  expect(screen.queryByText('New content')).not.toBeInTheDocument();
});

it('resets the failed state when resetKey changes to a meaningfully different tab or organization', () => {
  const { rerender } = render(
    <TabBoundary message="Section failed" retryLabel="Retry" resetKey="org-1:clubs"><Thrower /></TabBoundary>
  );
  expect(screen.getByRole('alert')).toBeVisible();

  rerender(
    <TabBoundary message="Section failed" retryLabel="Retry" resetKey="org-1:invoices"><div>New content</div></TabBoundary>
  );
  expect(screen.getByText('New content')).toBeVisible();
  expect(screen.queryByRole('alert')).not.toBeInTheDocument();
});
