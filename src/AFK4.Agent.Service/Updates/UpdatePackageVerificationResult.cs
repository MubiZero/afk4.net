namespace AFK4.Agent.Service.Updates;

public sealed record UpdatePackageVerificationResult(bool IsValid, string Message)
{
    public static UpdatePackageVerificationResult Valid(string message)
    {
        return new UpdatePackageVerificationResult(true, message);
    }

    public static UpdatePackageVerificationResult Invalid(string message)
    {
        return new UpdatePackageVerificationResult(false, message);
    }
}
