using System.Windows;

namespace RobloxMultiInstance.Views;

public partial class FriendUserWindow : Window
{
    public FriendUserWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
