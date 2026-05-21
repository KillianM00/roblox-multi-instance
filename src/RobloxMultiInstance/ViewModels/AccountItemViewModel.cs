using CommunityToolkit.Mvvm.ComponentModel;
using RobloxMultiInstance.Models;

namespace RobloxMultiInstance.ViewModels;

public partial class AccountItemViewModel : ObservableObject
{
    public Account Model { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _status = "Idle";
    [ObservableProperty] private bool _isBusy;

    public AccountItemViewModel(Account model)
    {
        Model = model;
    }

    public Guid Id => Model.Id;
    public string Alias => Model.Alias;
    public string Username => Model.Username ?? "(unknown)";
    public string DisplayName => Model.DisplayName ?? "";
    public string UserId => Model.UserId?.ToString() ?? "—";
    public string LastUsed => Model.LastUsedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—";

    public void NotifyModelChanged()
    {
        OnPropertyChanged(nameof(Alias));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(UserId));
        OnPropertyChanged(nameof(LastUsed));
    }
}
