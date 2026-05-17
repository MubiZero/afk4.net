using System.Windows;

namespace AFK4.GamingPc.Setup;

public partial class MainWindow : Window
{
    private readonly SetupShellViewModel viewModel;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = App.CreateViewModel();
        DataContext = viewModel;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            viewModel.Password = passwordBox.Password;
        }
    }
}
