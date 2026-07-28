namespace AFK4.OrganizationAdmin.App.Mvvm;

public interface IIdempotencyKeyFactory
{
    string Create(string operationName);
}

public sealed class GuidIdempotencyKeyFactory : IIdempotencyKeyFactory
{
    public string Create(string operationName)
    {
        return $"{operationName}-{Guid.NewGuid():N}";
    }
}
