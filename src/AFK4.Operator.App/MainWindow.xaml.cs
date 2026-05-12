using System.Windows;
using AFK4.Operator.App.FloorMap;

namespace AFK4.Operator.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
