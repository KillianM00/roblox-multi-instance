using System.Windows;
using System.Windows.Controls;
using RobloxMultiInstance.ViewModels;

namespace RobloxMultiInstance.Views;

public partial class AccountsView : UserControl
{
    public AccountsView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AccountsViewModel vm) vm.OnAccountSelectionChanged();
    }
}
