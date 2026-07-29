using System.IO;
using System.Windows;
using System.Windows.Threading;
using AFK4.OrganizationAdmin.App.Updates;

namespace AFK4.OrganizationAdmin.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private NamedPipeUpdateCoordinationServer? updateCoordinationServer;
    internal OrganizationAdminActivityState UpdateActivityState { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Backstop: an exception escaping a UI callback (e.g. the async-void bridge handler)
        // would otherwise tear down the WebView host with no record of why. Log it and keep the
        // dispatcher alive so a single bad message can't crash the app on every launch.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var secret = Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET");
        if (!string.IsNullOrWhiteSpace(secret))
        {
            var pipeName = Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_PIPE_NAME")
                ?? "afk4-organization-admin-updates";
            var acknowledgementPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AFK4",
                "OrganizationAdmin",
                "update-shutdown-acknowledgement.json");
            updateCoordinationServer = new NamedPipeUpdateCoordinationServer(
                pipeName,
                secret,
                UpdateActivityState,
                new FileOrganizationAdminShutdownAcknowledgementStore(acknowledgementPath),
                () => Dispatcher.BeginInvoke(Shutdown));
            updateCoordinationServer.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        updateCoordinationServer?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        OrganizationAdminStartupLog.Write("Unhandled dispatcher exception in Organization Admin host.", e.Exception);
        e.Handled = true;
    }
}
