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

    public bool IsValid => Model.IsValid;
    public string ValidityLabel => Model.LastValidatedAt == null
        ? "UNCHECKED"
        : (Model.IsValid
            ? $"VALID · {RelativeTime(Model.LastValidatedAt.Value)}"
            : $"LOGGED OUT · {RelativeTime(Model.LastValidatedAt.Value)}");

    private static string RelativeTime(DateTime utc)
    {
        var d = DateTime.UtcNow - utc;
        if (d.TotalMinutes < 1) return "just now";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes}m ago";
        if (d.TotalHours < 24) return $"{(int)d.TotalHours}h ago";
        return $"{(int)d.TotalDays}d ago";
    }

    public void NotifyModelChanged()
    {
        OnPropertyChanged(nameof(Alias));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(UserId));
        OnPropertyChanged(nameof(LastUsed));
        OnPropertyChanged(nameof(AvatarUrl));
        OnPropertyChanged(nameof(Initial));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ValidityLabel));
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
