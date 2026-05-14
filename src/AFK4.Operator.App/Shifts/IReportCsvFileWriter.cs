using System.IO;
using System.Text;
using Microsoft.Win32;

namespace AFK4.Operator.App.Shifts;

public interface IReportCsvFileWriter
{
    Task<string?> SaveAsync(
        string suggestedFileName,
        string csv,
        CancellationToken cancellationToken);
}

public sealed class SaveFileReportCsvFileWriter : IReportCsvFileWriter
{
    public async Task<string?> SaveAsync(
        string suggestedFileName,
        string csv,
        CancellationToken cancellationToken)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            DefaultExt = ".csv",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        await File.WriteAllTextAsync(dialog.FileName, csv, Encoding.UTF8, cancellationToken);
        return dialog.FileName;
    }
}

public sealed class UnconfiguredReportCsvFileWriter : IReportCsvFileWriter
{
    public Task<string?> SaveAsync(
        string suggestedFileName,
        string csv,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Report CSV file writer is not configured.");
    }
}
