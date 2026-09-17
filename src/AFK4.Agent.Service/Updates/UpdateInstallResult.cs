namespace AFK4.Agent.Service.Updates;

public sealed record UpdateInstallResult(bool Succeeded, string Message, bool RestartPending = false)
{
    public static UpdateInstallResult Success(string message)
    {
        return new UpdateInstallResult(true, message);
    }

    /// <summary>
    /// Установка прошла, но новая сборка заработает только после перезагрузки машины. Это не
    /// провал — откатывать нечего, — но и не «обновлено»: до перезагрузки ПК на старой версии.
    /// </summary>
    public static UpdateInstallResult RestartRequired(string message)
    {
        return new UpdateInstallResult(true, message, RestartPending: true);
    }

    public static UpdateInstallResult Failed(string message)
    {
        return new UpdateInstallResult(false, message);
    }
}
