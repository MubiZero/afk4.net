using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.GamingPc.Setup.Core;

namespace AFK4.GamingPc.Setup;

public sealed class SetupShellViewModel : INotifyPropertyChanged
{
    private readonly GamingPcSetupOrchestrator orchestrator;
    private readonly SetupEmbeddedResources resources;
    private string userName = string.Empty;
    private string password = string.Empty;
    private bool isBusy;
    private string finalStatus = "Ready.";

    public SetupShellViewModel(GamingPcSetupOrchestrator orchestrator, SetupEmbeddedResources resources)
    {
        this.orchestrator = orchestrator;
        this.resources = resources;
        InstallCommand = new AsyncRelayCommand(InstallAsync, CanInstall);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string EnvironmentLabel => $"Environment: {StagingSetupDefaults.EnvironmentName}";

    public string MachineLabel => $"Machine: {Environment.MachineName}";

    public ObservableCollection<SetupStepResult> Steps { get; } = [];

    public ICommand InstallCommand { get; }

    public string UserName
    {
        get => userName;
        set
        {
            if (SetField(ref userName, value))
            {
                RaiseCanExecuteChanged();
            }
        }
    }

    public string Password
    {
        get => password;
        set
        {
            if (SetField(ref password, value))
            {
                RaiseCanExecuteChanged();
            }
        }
    }

    public string FinalStatus
    {
        get => finalStatus;
        private set => SetField(ref finalStatus, value);
    }

    private bool CanInstall() =>
        !isBusy &&
        !string.IsNullOrWhiteSpace(UserName) &&
        !string.IsNullOrWhiteSpace(Password);

    private async Task InstallAsync()
    {
        isBusy = true;
        RaiseCanExecuteChanged();
        Steps.Clear();
        FinalStatus = "Installing.";

        try
        {
            var request = new SetupRequest(
                UserName.Trim(),
                Password,
                Environment.MachineName,
                resources.ReadLeasePublicKeyPem());

            var progress = new Progress<SetupStepResult>(Steps.Add);
            var result = await orchestrator.InstallAsync(request, progress);

            FinalStatus = result.Status switch
            {
                SetupStepStatus.Succeeded => $"Installed and enrolled. DeviceId: {result.DeviceId:D}",
                SetupStepStatus.Partial => $"Installed with pending verification. DeviceId: {result.DeviceId:D}",
                _ => "Installation failed."
            };
        }
        catch (Exception ex)
        {
            Steps.Add(SetupStepResult.Failed("Setup", "Setup failed.", ex.Message));
            FinalStatus = "Installation failed.";
        }
        finally
        {
            isBusy = false;
            RaiseCanExecuteChanged();
        }
    }

    private void RaiseCanExecuteChanged()
    {
        if (InstallCommand is AsyncRelayCommand command)
        {
            command.RaiseCanExecuteChanged();
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
