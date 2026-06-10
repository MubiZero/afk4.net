namespace AFK4.SetupWizard.Core;

/// <summary>
/// Fans the bootstrap config out to several writers (file + environment) in order.
/// </summary>
public sealed class CompositeBootstrapWriter(params ISetupWizardBootstrapWriter[] writers) : ISetupWizardBootstrapWriter
{
    private readonly IReadOnlyList<ISetupWizardBootstrapWriter> writers = writers;

    public void Write(SetupWizardBootstrapConfig config)
    {
        foreach (var writer in writers)
        {
            writer.Write(config);
        }
    }
}
