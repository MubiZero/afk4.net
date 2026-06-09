import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { LoginScreen } from './LoginScreen';

describe('LoginScreen', () => {
  it('calls onSubmit with entered credentials', async () => {
    let captured: { phone: string; password: string } | null = null;
    render(<LoginScreen onSubmit={async (phone, password) => { captured = { phone, password }; return true; }} />);

    fireEvent.change(screen.getByLabelText(/phone|телефон/i), { target: { value: '+992900000000' } });
    fireEvent.change(screen.getByLabelText(/password|пароль/i), { target: { value: 'secret' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in|войти/i }));

    await waitFor(() => expect(captured).toEqual({ phone: '+992900000000', password: 'secret' }));
  });

  it('shows an error when sign-in fails', async () => {
    render(<LoginScreen onSubmit={async () => false} />);
    fireEvent.change(screen.getByLabelText(/phone|телефон/i), { target: { value: 'x' } });
    fireEvent.change(screen.getByLabelText(/password|пароль/i), { target: { value: 'y' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in|войти/i }));
    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
  });
});
