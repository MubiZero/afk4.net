using Xunit;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Тест, которому нужна настоящая Windows: он зовёт `powershell.exe`, `signtool.exe` или строит
/// системные пути Windows. На macOS такой тест падает не по делу, а помеченный — честно
/// сообщается пропущенным, оставаясь при этом полноценной проверкой в Windows-джобе CI.
///
/// Помечать этим можно только по такому признаку. Тест, падающий по логике, отметка превращает
/// в невидимую дыру — а это хуже красного.
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows-only: зовёт инструменты Windows (powershell.exe / signtool.exe).";
        }
    }
}

/// <inheritdoc cref="WindowsOnlyFactAttribute"/>
public sealed class WindowsOnlyTheoryAttribute : TheoryAttribute
{
    public WindowsOnlyTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows-only: зовёт инструменты Windows (powershell.exe / signtool.exe).";
        }
    }
}
