using System.Windows;
using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard;

public partial class MainWindow : Window
{
    private readonly SetupWizardViewModel viewModel;

    public MainWindow(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        UpdatePanels();
    }

    private async void Discover_OnClick(object sender, RoutedEventArgs e)
    {
        await RunAsync(() => viewModel.DiscoverAsync(CancellationToken.None));
    }

    private void BranchList_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (BranchList.SelectedItem is SetupWizardBranchViewModel branch)
        {
            viewModel.SelectBranch(branch);
            UpdatePanels();
        }
    }

    private void Seat_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SetupWizardSeatViewModel seat })
        {
            viewModel.SelectSeat(seat);
            UpdatePanels();
        }
    }

    private async void CreateSeat_OnClick(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            await viewModel.CreateSeatAsync(NewSeatNameBox.Text, CancellationToken.None);
            if (viewModel.ErrorMessage is null)
            {
                NewSeatNameBox.Clear();
            }
        });
    }

    private void Role_OnChecked(object sender, RoutedEventArgs e)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.SelectedRole = ManagerRadio.IsChecked == true
            ? DeviceRoleNames.ManagerWorkstation
            : DeviceRoleNames.GamingPc;
    }

    private async void Enroll_OnClick(object sender, RoutedEventArgs e)
    {
        await RunAsync(() => viewModel.EnrollAsync(CancellationToken.None));
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsEnabled = false;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            viewModel.ReportError(ex.Message);
        }
        finally
        {
            IsEnabled = true;
            UpdatePanels();
        }
    }

    private void UpdatePanels()
    {
        OwnerCodePanel.Visibility = viewModel.CurrentStep == SetupWizardStep.OwnerCode
            ? Visibility.Visible
            : Visibility.Collapsed;
        BranchPanel.Visibility = viewModel.CurrentStep == SetupWizardStep.BranchSelection
            ? Visibility.Visible
            : Visibility.Collapsed;
        SeatPanel.Visibility = viewModel.CurrentStep == SetupWizardStep.SeatSelection
            ? Visibility.Visible
            : Visibility.Collapsed;
        RolePanel.Visibility = viewModel.CurrentStep == SetupWizardStep.RoleSelection
            ? Visibility.Visible
            : Visibility.Collapsed;
        FinishedPanel.Visibility = viewModel.CurrentStep == SetupWizardStep.Finished
            ? Visibility.Visible
            : Visibility.Collapsed;
        StepLabel.Text = viewModel.CurrentStep switch
        {
            SetupWizardStep.OwnerCode => "Step 1 of 4",
            SetupWizardStep.BranchSelection => "Step 2 of 4",
            SetupWizardStep.SeatSelection => "Step 3 of 4",
            SetupWizardStep.RoleSelection => "Step 4 of 4",
            _ => "Done"
        };
    }
}
