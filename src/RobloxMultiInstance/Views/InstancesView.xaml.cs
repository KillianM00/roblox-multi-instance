using System.Windows.Controls;
using System.Windows.Threading;
using RobloxMultiInstance.ViewModels;

namespace RobloxMultiInstance.Views;

public partial class InstancesView : UserControl
{
    public InstancesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is InstancesViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(InstancesViewModel.IsPlayMode) ||
                    args.PropertyName == nameof(InstancesViewModel.ActiveTile))
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (vm.IsPlayMode && PlayArea is not null)
                            vm.OnPlayAreaReady(PlayArea);
                    }), DispatcherPriority.Loaded);
                }
            };
            PlayArea.LayoutUpdated += (_, _) =>
            {
                if (vm.IsPlayMode && PlayArea.ActualWidth > 50 && PlayArea.ActualHeight > 50)
                    vm.OnPlayAreaReady(PlayArea);
            };
        }
    }
}
