using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AFK4.Localization;
using AFK4.Player.Shell.Shell;

namespace AFK4.Player.Shell.Overlay;

/// <summary>
/// Окно поверх игры (спека оболочки, §7): сообщение клуба и последняя минута. Не забирает фокус —
/// посреди боя окно, укравшее клавиатуру, хуже любого сообщения. Игры в исключительном
/// полноэкранном режиме его не покажут; это ограничение Windows, а не окна.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000;
    private const long WsExToolWindow = 0x00000080;

    private readonly ILocalizationService localization;

    public OverlayWindow(ILocalizationService localization)
    {
        this.localization = localization;
        InitializeComponent();
        SourceInitialized += (_, _) => MakeNonActivating();
        SizeChanged += (_, _) => PlaceAtTopCenter();
    }

    /// <summary>Игрок нажал «Продлить»: вывести оболочку вперёд.</summary>
    public event Action? ExtendRequested;

    public void Present(OverlayContent? content)
    {
        if (content is null)
        {
            if (IsVisible)
            {
                Hide();
            }

            return;
        }

        if (content.Kind == OverlayKind.ClubMessage)
        {
            HeadingText.Text = localization.T("playerShell.overlay.clubMessage");
            BodyText.Text = content.Text ?? string.Empty;
            ExtendButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            HeadingText.Text = localization.T("playerShell.overlay.lastMinute");
            BodyText.Text = RemainingTimeFormatter.Format(content.RemainingSeconds);
            ExtendButton.Content = localization.T("playerShell.overlay.extend");
            ExtendButton.Visibility = Visibility.Visible;
        }

        if (!IsVisible)
        {
            Show();
            PlaceAtTopCenter();
        }
    }

    private void OnExtendClick(object sender, RoutedEventArgs e) => ExtendRequested?.Invoke();

    private void PlaceAtTopCenter()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Top + 32;
    }

    private void MakeNonActivating()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style | WsExNoActivate | WsExToolWindow));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
