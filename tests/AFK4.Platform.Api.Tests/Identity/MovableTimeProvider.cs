namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>
/// Часы, которые двигает тест. Всё, что живёт временем — срок приглашения, кулдаун между кодами,
/// блокировка после неверных PIN, — иначе проверялось бы настоящим ожиданием.
/// </summary>
internal sealed class MovableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now = now.Add(by);
}
