import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { LoginScreen } from './LoginScreen';

describe('LoginScreen', () => {
  it('calls onSubmit with entered credentials', async () => {
    let captured: { phone: string; pin: string } | null = null;
    render(<LoginScreen onSubmit={async (phone, pin) => { captured = { phone, pin }; return true; }} />);

    fireEvent.change(screen.getByLabelText(/phone|телефон/i), { target: { value: '+992900000000' } });
    fireEvent.change(screen.getByLabelText(/pin/i), { target: { value: '1234' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in|войти/i }));

    await waitFor(() => expect(captured).toEqual({ phone: '+992900000000', pin: '1234' }));
  });

  it('shows an error when sign-in fails', async () => {
    render(<LoginScreen onSubmit={async () => false} />);
    fireEvent.change(screen.getByLabelText(/phone|телефон/i), { target: { value: 'x' } });
    fireEvent.change(screen.getByLabelText(/pin/i), { target: { value: '1234' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in|войти/i }));
    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
  });

  // Переход: экран объясняет сам и объясняет всем одинаково. Сервер причину отказа не называет —
  // иначе по этому экрану проверяли бы, у кого в сети есть аккаунт.
  it('explains the new PIN before anyone has even tried', () => {
    render(<LoginScreen onSubmit={async () => true} />);

    expect(screen.getByText(/PIN изменился/)).toBeInTheDocument();
    expect(screen.getByText(/в приложении/)).toBeInTheDocument();
  });

  it('offers both ways out after a refusal: the app and the desk', async () => {
    render(<LoginScreen onSubmit={async () => false} />);
    fireEvent.change(screen.getByLabelText(/phone|телефон/i), { target: { value: '+992900000000' } });
    fireEvent.change(screen.getByLabelText(/pin/i), { target: { value: '0000' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in|войти/i }));

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('в приложении');
    expect(alert.textContent).toContain('администратора');
  });

  // Один и тот же текст для любого номера: ветка «этому показать, тому нет» означала бы, что
  // сервер сказал оболочке, знаком ли ему номер.
  it('says exactly the same thing to every phone number', async () => {
    const first = render(<LoginScreen onSubmit={async () => false} />);
    fireEvent.change(screen.getByLabelText(/phone|телефон/i), { target: { value: '+992900000001' } });
    fireEvent.change(screen.getByLabelText(/pin/i), { target: { value: '1111' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in|войти/i }));
    const firstText = (await screen.findByRole('alert')).textContent;
    first.unmount();

    render(<LoginScreen onSubmit={async () => false} />);
    fireEvent.change(screen.getByLabelText(/phone|телефон/i), { target: { value: '+992900009999' } });
    fireEvent.change(screen.getByLabelText(/pin/i), { target: { value: '2222' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in|войти/i }));
    const secondText = (await screen.findByRole('alert')).textContent;

    expect(secondText).toBe(firstText);
  });
});
