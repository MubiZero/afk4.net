using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AFK4.WebHost;

/// <summary>
/// Маленькая карточка поверх всех окон: заголовок, текст и необязательная кнопка. Рисуется GDI, без движка: второй
/// Miniblink стоил бы десятки мегабайт ради трёх строк поверх игры. Фокус не забирает — посреди боя окно, укравшее
/// клавиатуру, хуже любого сообщения. Игры в исключительном полноэкранном режиме её не покажут: это ограничение Windows.
/// </summary>
public sealed unsafe class NativeCard
{
    private const string ClassName = "AFK4NativeCard";
    private const int Width = 420, MinWidth = 360, PaddingX = 22, PaddingY = 16, Radius = 14, Gap = 6, ButtonGap = 14, ButtonPadX = 18, ButtonPadY = 10, Top = 32;

    private static readonly Dictionary<IntPtr, NativeCard> ByWindow = [];
    private static double scale = 1;
    private static bool registered;

    private IntPtr handle;
    private string heading = string.Empty;
    private string body = string.Empty;
    private string? button;
    private Win32.Rect buttonRect;

    /// <summary>Нажата кнопка карточки.</summary>
    public event Action? ButtonClicked;

    public bool IsVisible { get; private set; }

    /// <summary>Из <see cref="WebHostWindow.Initialize"/>: класс окна регистрируется один раз за процесс.</summary>
    internal static void RegisterClass(double systemScale)
    {
        if (registered)
        {
            return;
        }

        scale = systemScale;
        fixed (char* className = ClassName)
        {
            var windowClass = new Win32.WndClassEx
            {
                Size = sizeof(Win32.WndClassEx),
                WndProc = &CardProcedure,
                Instance = Win32.GetModuleHandle(IntPtr.Zero),
                Cursor = Win32.LoadCursor(IntPtr.Zero, 32512),
                ClassName = className
            };
            if (Win32.RegisterClassEx(&windowClass) == 0)
            {
                throw new InvalidOperationException($"RegisterClassEx (card) failed: {Marshal.GetLastPInvokeError()}");
            }
        }

        registered = true;
    }

    /// <summary>Показать или обновить карточку (в потоке окон). Кнопка — если задан её текст.</summary>
    public void Show(string headingText, string bodyText, string? buttonText = null)
    {
        heading = headingText;
        body = bodyText;
        button = buttonText;
        if (handle == IntPtr.Zero)
        {
            handle = Win32.CreateWindowEx(Win32.WsExTopmost | Win32.WsExToolWindow | Win32.WsExNoActivate, ClassName, string.Empty, Win32.WsPopup,
                0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, Win32.GetModuleHandle(IntPtr.Zero), IntPtr.Zero);
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"CreateWindowEx (card) failed: {Marshal.GetLastPInvokeError()}");
            }

            ByWindow[handle] = this;
        }

        var (width, height) = Measure();
        var screenWidth = Win32.GetSystemMetrics(Win32.SmCxScreen);
        var x = (screenWidth - width) / 2;
        Win32.SetWindowPos(handle, Win32.HwndTopmost, x, Scaled(Top), width, height, Win32.SwpNoActivate);
        // Скруглённые углы: окно обрезается по области, а рамку рисует тот же контур.
        Win32.SetWindowRgn(handle, Win32.CreateRoundRectRgn(0, 0, width + 1, height + 1, Scaled(Radius) * 2, Scaled(Radius) * 2), 1);
        Win32.InvalidateRect(handle, null, 1);
        Win32.ShowWindow(handle, Win32.SwShowNoActivate);
        IsVisible = true;
    }

    public void Hide()
    {
        if (handle != IntPtr.Zero && IsVisible)
        {
            Win32.ShowWindow(handle, 0);
        }

        IsVisible = false;
    }

    public void Close()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ByWindow.Remove(handle);
        Win32.DestroyWindow(handle);
        handle = IntPtr.Zero;
        IsVisible = false;
    }

    private static int Scaled(int points) => (int)Math.Round(points * scale);

    // Высота по тексту: заголовок в строку, текст с переносом, ниже кнопка.
    private (int Width, int Height) Measure()
    {
        var width = Scaled(Width);
        var dc = Win32.GetDC(handle);
        var heading15 = Win32.SegoeUi(Scaled(15), 600);
        var body20 = Win32.SegoeUi(Scaled(20), 400);
        var label16 = Win32.SegoeUi(Scaled(16), 600);
        try
        {
            var inner = width - 2 * Scaled(PaddingX);
            var old = Win32.SelectObject(dc, heading15);
            var headingHeight = TextHeight(dc, heading, inner, Win32.DtNoPrefix | Win32.DtWordBreak);
            Win32.SelectObject(dc, body20);
            var bodyHeight = TextHeight(dc, body, inner, Win32.DtNoPrefix | Win32.DtWordBreak);
            var height = Scaled(PaddingY) + headingHeight + Scaled(Gap) + bodyHeight + Scaled(PaddingY);
            buttonRect = default;
            if (button is not null)
            {
                Win32.SelectObject(dc, label16);
                var label = new Win32.Rect();
                DrawText(dc, button, ref label, Win32.DtCalcRect | Win32.DtNoPrefix);
                var buttonWidth = label.Right - label.Left + 2 * Scaled(ButtonPadX);
                var buttonHeight = label.Bottom - label.Top + 2 * Scaled(ButtonPadY);
                var buttonTop = height - Scaled(PaddingY) + Scaled(ButtonGap);
                buttonRect = new Win32.Rect { Left = Scaled(PaddingX), Top = buttonTop, Right = Scaled(PaddingX) + buttonWidth, Bottom = buttonTop + buttonHeight };
                height = buttonTop + buttonHeight + Scaled(PaddingY);
            }

            Win32.SelectObject(dc, old);
            return (Math.Max(width, Scaled(MinWidth)), height);
        }
        finally
        {
            Win32.DeleteObject(heading15);
            Win32.DeleteObject(body20);
            Win32.DeleteObject(label16);
            Win32.ReleaseDC(handle, dc);
        }
    }

    private static int TextHeight(IntPtr dc, string text, int width, uint format)
    {
        var rect = new Win32.Rect { Left = 0, Top = 0, Right = width, Bottom = 0 };
        DrawText(dc, text, ref rect, format | Win32.DtCalcRect);
        return rect.Bottom - rect.Top;
    }

    private void Paint()
    {
        var paint = default(Win32.PaintStruct);
        var dc = Win32.BeginPaint(handle, &paint);
        var background = Win32.CreateSolidBrush(Win32.Colorref(0x0B1220));
        var border = Win32.CreateSolidBrush(Win32.Colorref(0x2A3140));
        var accent = Win32.CreateSolidBrush(Win32.Colorref(0x34D399));
        var heading15 = Win32.SegoeUi(Scaled(15), 600);
        var body20 = Win32.SegoeUi(Scaled(20), 400);
        var label16 = Win32.SegoeUi(Scaled(16), 600);
        try
        {
            Win32.Rect client;
            Win32.GetClientRect(handle, &client);
            var shape = Win32.CreateRoundRectRgn(0, 0, client.Right + 1, client.Bottom + 1, Scaled(Radius) * 2, Scaled(Radius) * 2);
            Win32.FillRgn(dc, shape, background);
            Win32.FrameRgn(dc, shape, border, 1, 1);
            Win32.DeleteObject(shape);

            Win32.SetBkMode(dc, Win32.Transparent);
            var inner = client.Right - 2 * Scaled(PaddingX);
            var old = Win32.SelectObject(dc, heading15);
            Win32.SetTextColor(dc, Win32.Colorref(0x9CA3AF));
            var headingHeight = TextHeight(dc, heading, inner, Win32.DtNoPrefix | Win32.DtWordBreak);
            var headingRect = new Win32.Rect { Left = Scaled(PaddingX), Top = Scaled(PaddingY), Right = Scaled(PaddingX) + inner, Bottom = Scaled(PaddingY) + headingHeight };
            DrawText(dc, heading, ref headingRect, Win32.DtNoPrefix | Win32.DtWordBreak);

            Win32.SelectObject(dc, body20);
            Win32.SetTextColor(dc, Win32.Colorref(0xF9FAFB));
            var bodyTop = headingRect.Bottom + Scaled(Gap);
            var bodyRect = new Win32.Rect { Left = Scaled(PaddingX), Top = bodyTop, Right = Scaled(PaddingX) + inner, Bottom = client.Bottom };
            DrawText(dc, body, ref bodyRect, Win32.DtNoPrefix | Win32.DtWordBreak);

            if (button is not null)
            {
                var plate = Win32.CreateRoundRectRgn(buttonRect.Left, buttonRect.Top, buttonRect.Right + 1, buttonRect.Bottom + 1, Scaled(8) * 2, Scaled(8) * 2);
                Win32.FillRgn(dc, plate, accent);
                Win32.DeleteObject(plate);
                Win32.SelectObject(dc, label16);
                Win32.SetTextColor(dc, Win32.Colorref(0x0B1220));
                var label = buttonRect;
                label.Top += Scaled(ButtonPadY);
                DrawText(dc, button, ref label, Win32.DtCenter | Win32.DtNoPrefix);
            }

            Win32.SelectObject(dc, old);
        }
        finally
        {
            Win32.DeleteObject(background);
            Win32.DeleteObject(border);
            Win32.DeleteObject(accent);
            Win32.DeleteObject(heading15);
            Win32.DeleteObject(body20);
            Win32.DeleteObject(label16);
            Win32.EndPaint(handle, &paint);
        }
    }

    private static void DrawText(IntPtr dc, string text, ref Win32.Rect rect, uint format)
    {
        fixed (char* chars = text)
        fixed (Win32.Rect* area = &rect)
        {
            Win32.DrawText(dc, chars, text.Length, area, format);
        }
    }

    private void Click(int x, int y)
    {
        if (button is not null && x >= buttonRect.Left && x < buttonRect.Right && y >= buttonRect.Top && y < buttonRect.Bottom)
        {
            ButtonClicked?.Invoke();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static IntPtr CardProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (ByWindow.TryGetValue(hwnd, out var card))
            {
                switch (message)
                {
                    case Win32.WmPaint:
                        card.Paint();
                        return IntPtr.Zero;
                    case Win32.WmEraseBackground:
                        return 1;
                    // Окно поверх игры не берёт фокус и от щелчка: кнопка срабатывает, а игра остаётся впереди.
                    case Win32.WmMouseActivate:
                        return Win32.MaNoActivate;
                    case Win32.WmLButtonDown:
                        card.Click((short)(lParam.ToInt64() & 0xFFFF), (short)((lParam.ToInt64() >> 16) & 0xFFFF));
                        return IntPtr.Zero;
                }
            }
        }
        catch
        {
            // Сбой отрисовки карточки не должен ронять экран игрока.
        }

        return Win32.DefWindowProc(hwnd, message, wParam, lParam);
    }
}
