using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxMultiInstance.Services.Network;

namespace RobloxMultiInstance.ViewModels;

public partial class NetworkViewModel : ObservableObject
{
    public ObservableCollection<IVpnProvider> DetectedProviders { get; } = new();
    public ObservableCollection<IVpnProvider> InstallSuggestions { get; } = new();
    public ObservableCollection<VpnLocation> Locations { get; } = new();

    [ObservableProperty] private IVpnProvider? _selectedProvider;
    [ObservableProperty] private VpnLocation? _selectedLocation;
    [ObservableProperty] private string? _publicIp;
    [ObservableProperty] private string? _previousPublicIp;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string? _currentLocation;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _anyDetected;

    public NetworkViewModel()
    {
        ReloadDetection();
        _ = RefreshAsync();
    }

    private void ReloadDetection()
    {
        DetectedProviders.Clear();
        foreach (var p in VpnDetector.DetectAll()) DetectedProviders.Add(p);
        AnyDetected = DetectedProviders.Count > 0;

        InstallSuggestions.Clear();
        if (!AnyDetected)
            foreach (var s in VpnDetector.AllKnown()) InstallSuggestions.Add(s);

        if (SelectedProvider == null || !DetectedProviders.Contains(SelectedProvider))
            SelectedProvider = DetectedProviders.FirstOrDefault();
    }

    partial void OnSelectedProviderChanged(IVpnProvider? value)
    {
        _ = ReloadLocationsAsync();
    }

    private async Task ReloadLocationsAsync()
    {
        Locations.Clear();
        if (SelectedProvider is null || !SelectedProvider.IsInstalled) return;
        try
        {
            var locs = await SelectedProvider.ListLocationsAsync();
            foreach (var l in locs) Locations.Add(l);
        }
        catch { }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = "Probing public IP...";
        try
        {
            var newIp = await PublicIpClient.GetAsync();
            if (newIp != null && newIp != PublicIp) PreviousPublicIp = PublicIp;
            PublicIp = newIp ?? "(unreachable)";

            if (SelectedProvider != null && SelectedProvider.IsInstalled)
            {
                var status = await SelectedProvider.GetStatusAsync();
                IsConnected = status.Connected;
                CurrentLocation = status.Location;
                StatusText = status.Connected
                    ? $"{SelectedProvider.Name}: connected{(status.Location != null ? $" — {status.Location}" : "")}"
                    : $"{SelectedProvider.Name}: disconnected";
            }
            else
            {
                IsConnected = false;
                CurrentLocation = null;
                StatusText = SelectedProvider == null ? "No VPN detected" : $"{SelectedProvider.Name}: not installed";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RedetectAsync()
    {
        ReloadDetection();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedProvider is null || !SelectedProvider.IsInstalled) return;
        IsBusy = true;
        StatusText = "Connecting...";
        try
        {
            await SelectedProvider.ConnectAsync(SelectedLocation?.Id);
            await Task.Delay(2500);
        }
        finally
        {
            IsBusy = false;
        }
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        if (SelectedProvider is null || !SelectedProvider.IsInstalled) return;
        IsBusy = true;
        StatusText = "Disconnecting...";
        try
        {
            await SelectedProvider.DisconnectAsync();
            await Task.Delay(1500);
        }
        finally
        {
            IsBusy = false;
        }
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task CycleAsync()
    {
        if (SelectedProvider is null || !SelectedProvider.IsInstalled) return;
        IsBusy = true;
        StatusText = "Cycling exit...";
        try
        {
            await SelectedProvider.CycleAsync();
            await Task.Delay(3000);
        }
        finally
        {
            IsBusy = false;
        }
        await RefreshAsync();
    }

    [RelayCommand]
    private void OpenDownload(IVpnProvider? p)
    {
        if (p == null || string.IsNullOrEmpty(p.DownloadUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = p.DownloadUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't open browser: {ex.Message}");
        }
    }
}
