using Xunit;

namespace AFK4.SetupWizard.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> that only runs on Windows. Some components build Windows system
/// paths (powershell.exe, Program Files) that don't resolve on the Linux/WSL dev gate; this keeps
/// the test meaningful in Windows CI while reporting it as skipped elsewhere instead of failing.
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows-only: builds Windows system paths that don't resolve on Linux.";
        }
    }
}
