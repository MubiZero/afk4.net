using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Launcher;

public sealed class LauncherAppViewModel(LauncherAppDto dto)
{
    public string AppId { get; } = dto.AppId;

    public string DisplayName { get; } = dto.DisplayName;

    public string Category { get; } = dto.Category;

    public string? IconUri { get; } = dto.IconUri;

    public bool IsAvailable { get; } = dto.IsAvailable;
}
