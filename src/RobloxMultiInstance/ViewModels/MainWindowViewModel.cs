using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RobloxMultiInstance.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _currentTab = "Accounts";
    [ObservableProperty] private bool _mutexHeld;

    public MainWindowViewModel()
    {
        MutexHeld = App.MutexHolder.IsHeld;
    }

    [RelayCommand]
    private void GoToAccounts() => CurrentTab = "Accounts";

    [RelayCommand]
    private void GoToInstances() => CurrentTab = "Instances";

    [RelayCommand]
    private void GoToNetwork() => CurrentTab = "Network";

    [RelayCommand]
    private void GoToGenerate() => CurrentTab = "Generate";
}
