namespace AFK4.GamingPc.Setup.Core;

public sealed record SetupStepResult(
    string Step,
    SetupStepStatus Status,
    string Message,
    string? Detail = null)
{
    public static SetupStepResult Running(string step, string message) => new(step, SetupStepStatus.Running, message);

    public static SetupStepResult Succeeded(string step, string message, string? detail = null) =>
        new(step, SetupStepStatus.Succeeded, message, detail);

    public static SetupStepResult Failed(string step, string message, string? detail = null) =>
        new(step, SetupStepStatus.Failed, message, detail);

    public static SetupStepResult Partial(string step, string message, string? detail = null) =>
        new(step, SetupStepStatus.Partial, message, detail);
}
