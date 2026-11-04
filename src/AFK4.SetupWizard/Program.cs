using AFK4.SetupWizard;
using AFK4.SetupWizard.Core;
using AFK4.SetupWizard.Core.SilentInstall;
using AFK4.SetupWizard.Web;
using AFK4.WebHost;

return SetupWizardProgram.Run(args);

internal static class SetupWizardProgram
{
    private const string RemoveKioskArgument = "--remove-kiosk";
    private const string Caption = "Мастер установки AFK4.NET";

    [STAThread]
    public static int Run(string[] args)
    {
        if (args.Contains(RemoveKioskArgument))
        {
            return RemoveKiosk();
        }

        var silentInstall = SilentInstallOptions.Parse(args);
        if (silentInstall.Requested)
        {
            return RunSilentInstall(silentInstall);
        }

        try
        {
            return RunWindow(args);
        }
        catch (Exception exception)
        {
            // Без окна WPF некому показать сбой: пишем журнал и говорим человеку, а не падаем молча.
            SetupWizardStartupLog.Write("Unhandled error in the setup wizard.", exception);
            NativeDialogs.ShowError(Caption, $"Произошла непредвиденная ошибка мастера установки:\n\n{exception.Message}");
            return 1;
        }
    }

    private static int RunWindow(string[] args)
    {
#if DEBUG
        if (args.Contains("--preview"))
        {
            var previewMachine = new SetupWizardMachineInfo("PREVIEW-PC");
            var previewBridge = new SetupWizardWebHostBridge(
                AFK4.SetupWizard.Preview.PreviewSetupWizard.CreateApiClient(),
                AFK4.SetupWizard.Preview.PreviewSetupWizard.CreateDeviceKeyStore(),
                AFK4.SetupWizard.Preview.PreviewSetupWizard.CreateBootstrapWriter(),
                previewMachine,
                AFK4.SetupWizard.Preview.PreviewSetupWizard.CreateCompletionAction(),
                AFK4.SetupWizard.Preview.PreviewSetupWizard.CreateShellProvisioner(),
                AFK4.SetupWizard.Preview.PreviewSetupWizard.CreateShellProvisioner(),
                AFK4.SetupWizard.Preview.PreviewSetupWizard.CreateOperatorLauncher());
            return ShowWindow(previewBridge, previewMachine, isPreview: true);
        }
#endif

        if (!ElevationGuard.EnsureElevated())
        {
            return 0;
        }

        var machine = new WizardMachine();
        var bridge = new SetupWizardWebHostBridge(
            machine.ApiClient,
            machine.KeyStore,
            machine.BootstrapWriter,
            machine.Info,
            machine.CompletionAction,
            machine.ShellProvisioner,
            machine.OperatorProvisioner,
            machine.OperatorLauncher,
            new NativeLogoPicker(),
            machine.Kiosk,
            new ShutdownRebootAction(machine.ProcessRunner));
        return ShowWindow(bridge, machine.Info, isPreview: false);
    }

    private static int ShowWindow(SetupWizardWebHostBridge bridge, SetupWizardMachineInfo machine, bool isPreview)
    {
        WebHostWindow.Initialize();
        new SetupWizardWindow(
            SetupWizardWebShellOptions.LoadFromEnvironment(),
            new SetupWizardWebAssetResolver(AppContext.BaseDirectory),
            bridge,
            machine,
            SetupWizardDefaults.PlatformBaseUrl,
            isPreview).Show();
        return WebHostWindow.RunMessageLoop();
    }

    private static int RemoveKiosk()
    {
        try
        {
            new WizardMachine().Kiosk.Remove();
            return 0;
        }
        catch (Exception exception)
        {
            SetupWizardStartupLog.Write("Kiosk removal during uninstall failed.", exception);
            return 1;
        }
    }

    private static int RunSilentInstall(SilentInstallParse silentInstall)
    {
        if (silentInstall.Options is null)
        {
            SetupWizardStartupLog.Write($"Silent install could not start: {silentInstall.Error}");
            return SilentInstallExitCodes.BadArguments;
        }

        if (!ElevationGuard.IsElevated())
        {
            SetupWizardStartupLog.Write("Silent install needs administrator rights: run the installer from an elevated deployment tool.");
            return SilentInstallExitCodes.NotElevated;
        }

        var machine = new WizardMachine();
        var installer = new SilentInstaller(
            machine.ApiClient,
            machine.KeyStore,
            machine.Info,
            new SetupWizardDeviceSetup(
                machine.BootstrapWriter,
                machine.CompletionAction,
                machine.ShellProvisioner,
                machine.OperatorProvisioner,
                machine.OperatorLauncher,
                machine.Kiosk));

        try
        {
            // Окна нет, и ждать в потоке запуска можно: ни одно сообщение ему не придёт.
            return Task.Run(() => installer.RunAsync(silentInstall.Options, CancellationToken.None)).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            SetupWizardStartupLog.Write("Silent install failed unexpectedly.", exception);
            return SilentInstallExitCodes.SetupFailed;
        }
    }
}
