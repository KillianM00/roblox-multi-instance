using System.Windows;
using RobloxMultiInstance.ViewModels;

namespace RobloxMultiInstance;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.OnAccountSelectionChanged();
    }
}
