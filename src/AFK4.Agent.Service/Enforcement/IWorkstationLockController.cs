namespace AFK4.Agent.Service.Enforcement;

/// <summary>Что именно удалось сделать с машиной. Пустой список означает «ничего».</summary>
public sealed record WorkstationLockOutcome(IReadOnlyList<string> Enforced)
{
    public static readonly WorkstationLockOutcome Nothing = new([]);

    public bool IsEnforced => Enforced.Count > 0;

    public string Describe() => IsEnforced ? string.Join(", ", Enforced) : "nothing";
}

public interface IWorkstationLockController
{
    Task<WorkstationLockOutcome> LockAsync(CancellationToken cancellationToken);

    Task<WorkstationLockOutcome> UnlockAsync(CancellationToken cancellationToken);
}
