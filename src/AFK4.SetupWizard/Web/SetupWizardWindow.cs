using System.Text.Json;
using AFK4.SetupWizard.Core;
using AFK4.WebHost;

namespace AFK4.SetupWizard.Web;

/// <summary>
/// Окно мастера: экран мастера на Miniblink без рамки Windows — заголовок и кнопки рисует сам экран и шлёт команды
/// окна (window:drag, window:minimize…). Остальные сообщения — запросы к мосту мастера (SetupWizardWebHostBridge).
/// </summary>
internal sealed class SetupWizardWindow
{
    private const string StartupFailureTitle = "Не удалось запустить мастер установки";

    private readonly SetupWizardWebHostBridge hostBridge;
    private readonly WebHostWindow window;

    public SetupWizardWindow(
        SetupWizardWebShellOptions shellOptions,
        SetupWizardWebAssetResolver assetResolver,
        SetupWizardWebHostBridge hostBridge,
        SetupWizardMachineInfo machineInfo,
        Uri platformBaseUrl,
        bool isPreview)
    {
        this.hostBridge = hostBridge;
        var launchTarget = assetResolver.Resolve(shellOptions);
        window = new WebHostWindow(new WebHostOptions
        {
            Title = "Мастер установки AFK4.NET",
            ContentRoot = launchTarget.LocalFolderPath ?? AppContext.BaseDirectory,
            VirtualHost = SetupWizardWebAssetResolver.LocalVirtualHost,
            DevServerUrl = launchTarget.UsesLocalFolder ? null : launchTarget.Source,
            InitScript = SetupWizardWebBootstrapScript.Create(launchTarget, machineInfo, platformBaseUrl, isPreview),
            DataFolder = SetupWizardDataFolder.Resolve(),
            Width = 1100,
            Height = 740,
            MinWidth = 960,
            MinHeight = 640,
            Frameless = true
        });
        window.MessageReceived += OnMessage;
        // Экран рисует кнопку «развернуть/вернуть» по состоянию, которое сообщает окно, — оно меняется и без кнопки:
        // двойной щелчок по заголовку, Win+↑, прилипание к краю.
        window.MaximizedChanged += maximized => Post(new SetupWizardWindowMessage("window:state", Maximized: maximized));
        window.Diagnostic += text => SetupWizardStartupLog.Write("Setup wizard window: " + text);
    }

    public void Show() => window.Show();

    private async void OnMessage(string json)
    {
        // async void: исключение здесь уронило бы мастер посреди регистрации ПК. Известные сбои мост сам превращает
        // в ответы с ошибкой; здесь — только непредвиденное.
        try
        {
            if (TryHandleWindowMessage(json))
            {
                return;
            }

            var response = await hostBridge.HandleAsync(json, CancellationToken.None);
            if (response is not null)
            {
                window.PostMessageAsJson(response);
            }
        }
        catch (Exception exception)
        {
            SetupWizardStartupLog.Write("Unhandled error while processing a host bridge message.", exception);
            window.ShowStatus(StartupFailureTitle, $"Произошла непредвиденная ошибка: {exception.Message}");
        }
    }

    private bool TryHandleWindowMessage(string json)
    {
        SetupWizardWindowMessage? message;
        try
        {
            message = JsonSerializer.Deserialize(json, SetupWizardWebJson.Default.SetupWizardWindowMessage);
        }
        catch (JsonException)
        {
            return true;
        }

        switch (message?.Type)
        {
            case "window:drag":
                window.BeginDrag();
                return true;
            case "window:minimize":
                window.Minimize();
                return true;
            case "window:toggleMaximize":
                window.ToggleMaximize();
                return true;
            case "window:close":
                window.Close();
                return true;
            case "window:theme":
                // Иконка окна — из exe; смена светлой/тёмной иконки WPF-окна в новое окно не переехала.
                return true;
            default:
                return false;
        }
    }

    private void Post(SetupWizardWindowMessage message) =>
        window.PostMessageAsJson(JsonSerializer.Serialize(message, SetupWizardWebJson.Default.SetupWizardWindowMessage));
}
