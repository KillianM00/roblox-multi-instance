using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxMultiInstance.Models;
using RobloxMultiInstance.Services;

namespace RobloxMultiInstance.ViewModels;

public partial class InstanceTileViewModel : ObservableObject
{
    public RobloxInstance Instance { get; }

    [ObservableProperty] private string _alias = "";
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _windowTitle = "";
    [ObservableProperty] private IntPtr _hwnd;
    [ObservableProperty] private bool _hasHwnd;
    [ObservableProperty] private bool _isInPlayMode;
    [ObservableProperty] private string _accountTagline = "";

    public int Pid => Instance.Pid;

    public InstanceTileViewModel(RobloxInstance inst, Models.Account? account)
    {
        Instance = inst;
        Refresh(inst, account);
    }

    public void Refresh(RobloxInstance inst, Models.Account? account)
    {
        Hwnd = inst.Hwnd;
        HasHwnd = inst.Hwnd != IntPtr.Zero;
        WindowTitle = string.IsNullOrEmpty(inst.Title) ? "Roblox" : inst.Title;
        IsInPlayMode = inst.InPlayMode;

        if (account != null)
        {
            Alias = account.Alias;
            Username = account.Username ?? "";
            AccountTagline = string.IsNullOrEmpty(account.Username)
                ? account.Alias
                : $"{account.Alias} · @{account.Username}";
        }
        else
        {
            Alias = $"Unlinked (PID {inst.Pid})";
            Username = "";
            AccountTagline = $"PID {inst.Pid}";
        }
    }

    [RelayCommand]
    private void Focus()
    {
        if (Hwnd != IntPtr.Zero) WindowManager.Focus(Hwnd);
    }

    [RelayCommand]
    private void Minimize()
    {
        if (Hwnd != IntPtr.Zero) WindowManager.Minimize(Hwnd);
    }
}
