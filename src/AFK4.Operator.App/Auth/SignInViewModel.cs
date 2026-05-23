using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Shell;

namespace AFK4.Operator.App.Auth;

public sealed class SignInViewModel : INotifyPropertyChanged
{
    private readonly IOperatorAuthApiClient authApiClient;
    private string organizationIdText = "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08";
    private string userName = string.Empty;
    private string password = string.Empty;
    private string statusMessage = string.Empty;
    private string? errorMessage;
    private bool isBusy;

    public SignInViewModel(IOperatorAuthApiClient authApiClient)
    {
        this.authApiClient = authApiClient;
        SignInCommand = new AsyncRelayCommand(SignInAsync, CanRunCommand);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<OperatorUserContext>? SignedIn;

    public AsyncRelayCommand SignInCommand { get; }

    public string OrganizationIdText
    {
        get => organizationIdText;
        set => SetField(ref organizationIdText, value);
    }

    public string UserName
    {
        get => userName;
        set => SetField(ref userName, value);
    }

    public string Password
    {
        get => password;
        set => SetField(ref password, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                SignInCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Task SignInAsync(CancellationToken cancellationToken)
    {
        ClearMessages();

        if (!Guid.TryParse(OrganizationIdText, out var organizationId))
        {
            ErrorMessage = "OrganizationId должен быть корректным GUID.";
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            ErrorMessage = "Укажите имя пользователя.";
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Укажите пароль.";
            return Task.CompletedTask;
        }

        return RunSignInAsync(organizationId, cancellationToken);
    }

    private async Task RunSignInAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            var response = await authApiClient.SignInAsync(
                organizationId,
                UserName.Trim(),
                Password,
                cancellationToken);

            var branchId = response.BranchIds.FirstOrDefault();
            if (branchId == Guid.Empty)
            {
            ErrorMessage = "У сотрудника нет назначенного филиала.";
                return;
            }

            var permissions = response.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var context = new OperatorUserContext(
                response.StaffUserId,
                response.OrganizationId,
                branchId,
                response.DisplayName,
                permissions);

            StatusMessage = $"Вход выполнен: {response.DisplayName}.";
            SignedIn?.Invoke(context);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRunCommand()
    {
        return !IsBusy;
    }

    private void ClearMessages()
    {
        ErrorMessage = null;
        StatusMessage = string.Empty;
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
