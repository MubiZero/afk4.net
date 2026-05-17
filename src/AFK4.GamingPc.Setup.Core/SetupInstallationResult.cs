namespace AFK4.GamingPc.Setup.Core;

public sealed record SetupInstallationResult(
    SetupStepStatus Status,
    Guid? DeviceId,
    IReadOnlyList<SetupStepResult> Steps)
{
    public bool IsSuccess => Status == SetupStepStatus.Succeeded;

    public bool IsPartial => Status == SetupStepStatus.Partial;
}
