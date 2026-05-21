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
    public string? AvatarUrl => Model.AvatarUrl;
    public string Initial => string.IsNullOrEmpty(Model.Alias) ? "?" : Model.Alias[..1].ToUpperInvariant();

    public void NotifyModelChanged()
    {
        OnPropertyChanged(nameof(Alias));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(UserId));
        OnPropertyChanged(nameof(LastUsed));
        OnPropertyChanged(nameof(AvatarUrl));
        OnPropertyChanged(nameof(Initial));
    }

    public bool KillTrackedInstance()
    {
        var inst = App.ProcessTracker.FindByAccount(Id);
        if (inst == null)
        {
            Status = "Not running";
            return false;
        }
        if (App.ProcessTracker.KillByPid(inst.Pid))
        {
            Status = "Killed";
            return true;
        }
        Status = "Kill failed";
        return false;
    }
}
