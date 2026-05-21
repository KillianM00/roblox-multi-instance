using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxMultiInstance.Services;

namespace RobloxMultiInstance.ViewModels;

public partial class InstancesViewModel : ObservableObject
{
    private readonly Dispatcher _dispatcher;

    public ObservableCollection<InstanceTileViewModel> Tiles { get; } = new();

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private InstanceTileViewModel? _activeTile;
    [ObservableProperty] private bool _isPlayMode;

    public InstancesViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        App.ProcessTracker.InstancesChanged += OnInstancesChanged;
        Refresh();
    }

    private void OnInstancesChanged()
    {
        if (_dispatcher.CheckAccess()) Refresh();
        else _dispatcher.BeginInvoke(new Action(Refresh));
    }

    private void Refresh()
    {
        var instances = App.ProcessTracker.Instances;

        for (int i = Tiles.Count - 1; i >= 0; i--)
        {
            if (!instances.Any(x => x.Pid == Tiles[i].Pid))
            {
                if (ActiveTile == Tiles[i])
                {
                    ActiveTile = null;
                    IsPlayMode = false;
                }
                Tiles.RemoveAt(i);
            }
        }

        foreach (var inst in instances)
        {
            var account = inst.AccountId.HasValue ? App.AccountStore.Find(inst.AccountId.Value) : null;
            var existing = Tiles.FirstOrDefault(t => t.Pid == inst.Pid);
            if (existing != null)
            {
                existing.Refresh(inst, account);
            }
            else
            {
                Tiles.Add(new InstanceTileViewModel(inst, account));
            }
        }

        StatusText = instances.Count == 0
            ? "No Roblox instances running. Launch some from the Accounts tab."
            : $"{instances.Count} Roblox instance{(instances.Count == 1 ? "" : "s")} running";
    }

    [RelayCommand]
    private void Activate(InstanceTileViewModel? tile)
    {
        if (tile == null) return;
        ActiveTile = tile;
        IsPlayMode = true;
    }

    [RelayCommand]
    private void BackToGrid()
    {
        if (ActiveTile != null)
        {
            WindowManager.ExitPlayMode(ActiveTile.Instance);
            ActiveTile.IsInPlayMode = false;
        }
        ActiveTile = null;
        IsPlayMode = false;
    }

    [RelayCommand]
    private void ArrangeOnDesktop()
    {
        var hwnds = Tiles.Select(t => t.Hwnd).Where(h => h != IntPtr.Zero).ToList();
        WindowManager.ArrangeOnPrimaryMonitor(hwnds);
    }

    [RelayCommand]
    private void MinimizeAll()
    {
        foreach (var t in Tiles)
            if (t.Hwnd != IntPtr.Zero) WindowManager.Minimize(t.Hwnd);
    }

    [RelayCommand]
    private void KillAll()
    {
        if (Tiles.Count == 0) return;
        var r = MessageBox.Show($"Kill all {Tiles.Count} running Roblox instance(s)?",
            "Confirm kill all", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        App.ProcessTracker.KillAll();
    }

    public void OnPlayAreaReady(FrameworkElement playArea)
    {
        if (ActiveTile == null || !IsPlayMode) return;
        WindowManager.EnterPlayMode(ActiveTile.Instance, playArea);
        ActiveTile.IsInPlayMode = true;
    }
}
