using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using RobloxMultiInstance.Services;

namespace RobloxMultiInstance.Controls;

public class DwmThumbnailHost : FrameworkElement
{
    public static readonly DependencyProperty SourceHwndProperty = DependencyProperty.Register(
        nameof(SourceHwnd), typeof(IntPtr), typeof(DwmThumbnailHost),
        new PropertyMetadata(IntPtr.Zero, OnSourceHwndChanged));

    public IntPtr SourceHwnd
    {
        get => (IntPtr)GetValue(SourceHwndProperty);
        set => SetValue(SourceHwndProperty, value);
    }

    public Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(Brush), typeof(DwmThumbnailHost),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    private IntPtr _thumbHandle = IntPtr.Zero;
    private IntPtr _destHwnd = IntPtr.Zero;
    private Window? _hostWindow;
    private bool _registered;

    public DwmThumbnailHost()
    {
        Focusable = false;
        IsHitTestVisible = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdateThumbnailProperties();
        LayoutUpdated += (_, _) => UpdateThumbnailProperties();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is bool v && v) UpdateThumbnailProperties();
            else HideThumbnail();
        };
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Background != null)
            dc.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hostWindow = Window.GetWindow(this);
        if (_hostWindow == null) return;
        _destHwnd = new WindowInteropHelper(_hostWindow).Handle;
        if (_destHwnd == IntPtr.Zero)
            _hostWindow.SourceInitialized += HostOnSourceInitialized;
        _hostWindow.SizeChanged += HostOnSizeChanged;
        _hostWindow.LocationChanged += HostOnLocationChanged;
        TryRegister();
    }

    private void HostOnSourceInitialized(object? sender, EventArgs e)
    {
        if (_hostWindow == null) return;
        _hostWindow.SourceInitialized -= HostOnSourceInitialized;
        _destHwnd = new WindowInteropHelper(_hostWindow).Handle;
        TryRegister();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unregister();
        if (_hostWindow != null)
        {
            _hostWindow.SizeChanged -= HostOnSizeChanged;
            _hostWindow.LocationChanged -= HostOnLocationChanged;
            _hostWindow.SourceInitialized -= HostOnSourceInitialized;
        }
    }

    private void HostOnSizeChanged(object? sender, EventArgs e) => UpdateThumbnailProperties();
    private void HostOnLocationChanged(object? sender, EventArgs e) => UpdateThumbnailProperties();

    private static void OnSourceHwndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DwmThumbnailHost host) return;
        host.Unregister();
        if (host.IsLoaded) host.TryRegister();
    }

    private void TryRegister()
    {
        if (_registered) return;
        if (_destHwnd == IntPtr.Zero) return;
        var src = SourceHwnd;
        if (src == IntPtr.Zero) return;
        if (!Win32.IsWindow(src)) return;
        try
        {
            var hr = Dwm.DwmRegisterThumbnail(_destHwnd, src, out _thumbHandle);
            if (hr == 0 && _thumbHandle != IntPtr.Zero)
            {
                _registered = true;
                UpdateThumbnailProperties();
            }
        }
        catch
        {
            _thumbHandle = IntPtr.Zero;
            _registered = false;
        }
    }

    private void Unregister()
    {
        if (_thumbHandle != IntPtr.Zero)
        {
            try { Dwm.DwmUnregisterThumbnail(_thumbHandle); } catch { }
            _thumbHandle = IntPtr.Zero;
        }
        _registered = false;
    }

    private FrameworkElement? FindClipRegion()
    {
        DependencyObject? cur = this;
        while (cur != null)
        {
            cur = VisualTreeHelper.GetParent(cur);
            if (cur is ScrollViewer sv) return sv;
            if (cur is UserControl uc) return uc;
        }
        return _hostWindow;
    }

    private void UpdateThumbnailProperties()
    {
        if (!_registered || _thumbHandle == IntPtr.Zero || _hostWindow == null) return;
        if (!IsVisible)
        {
            HideThumbnail();
            return;
        }
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            HideThumbnail();
            return;
        }

        try
        {
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source == null) return;
            var root = source.RootVisual;
            if (root == null) return;

            var transform = source.CompositionTarget?.TransformToDevice ?? Matrix.Identity;

            var topLeftDip = this.TransformToAncestor(root).Transform(new Point(0, 0));
            var bottomRightDip = this.TransformToAncestor(root).Transform(new Point(ActualWidth, ActualHeight));
            var tl = transform.Transform(topLeftDip);
            var br = transform.Transform(bottomRightDip);

            var idealDest = new Rect(tl, br);

            // Letterbox to source aspect ratio
            int hr = Dwm.DwmQueryThumbnailSourceSize(_thumbHandle, out var size);
            if (hr == 0 && size.cx > 0 && size.cy > 0)
            {
                double destAspect = idealDest.Width / Math.Max(1, idealDest.Height);
                double srcAspect = (double)size.cx / size.cy;
                if (Math.Abs(destAspect - srcAspect) > 0.01)
                {
                    if (destAspect > srcAspect)
                    {
                        double newW = idealDest.Height * srcAspect;
                        double offset = (idealDest.Width - newW) / 2;
                        idealDest = new Rect(idealDest.Left + offset, idealDest.Top, newW, idealDest.Height);
                    }
                    else
                    {
                        double newH = idealDest.Width / srcAspect;
                        double offset = (idealDest.Height - newH) / 2;
                        idealDest = new Rect(idealDest.Left, idealDest.Top + offset, idealDest.Width, newH);
                    }
                }
            }

            // Clip to nearest meaningful clip region (ScrollViewer or UserControl)
            var clipRegion = FindClipRegion();
            Rect clipRectPx = new Rect(double.NegativeInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity);
            if (clipRegion != null && clipRegion.ActualWidth > 0 && clipRegion.ActualHeight > 0
                && PresentationSource.FromVisual(clipRegion) != null)
            {
                try
                {
                    var crTL = clipRegion.TransformToAncestor(root).Transform(new Point(0, 0));
                    var crBR = clipRegion.TransformToAncestor(root).Transform(new Point(clipRegion.ActualWidth, clipRegion.ActualHeight));
                    var ctl = transform.Transform(crTL);
                    var cbr = transform.Transform(crBR);
                    clipRectPx = new Rect(ctl, cbr);
                }
                catch { }
            }

            var clipped = Rect.Intersect(idealDest, clipRectPx);
            if (clipped.IsEmpty || clipped.Width < 6 || clipped.Height < 6)
            {
                HideThumbnail();
                return;
            }

            // Build source rect that maps to the clipped portion (so no distortion)
            double xRatio = (size.cx > 0 && idealDest.Width > 0) ? (size.cx / idealDest.Width) : 1.0;
            double yRatio = (size.cy > 0 && idealDest.Height > 0) ? (size.cy / idealDest.Height) : 1.0;

            var src = new Win32.RECT
            {
                Left = (int)Math.Round((clipped.Left - idealDest.Left) * xRatio),
                Top = (int)Math.Round((clipped.Top - idealDest.Top) * yRatio),
                Right = (int)Math.Round((clipped.Right - idealDest.Left) * xRatio),
                Bottom = (int)Math.Round((clipped.Bottom - idealDest.Top) * yRatio),
            };

            var dest = new Win32.RECT
            {
                Left = (int)Math.Round(clipped.Left),
                Top = (int)Math.Round(clipped.Top),
                Right = (int)Math.Round(clipped.Right),
                Bottom = (int)Math.Round(clipped.Bottom),
            };

            var props = new Dwm.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = Dwm.DWM_TNP_VISIBLE | Dwm.DWM_TNP_RECTDESTINATION
                          | Dwm.DWM_TNP_OPACITY | Dwm.DWM_TNP_SOURCECLIENTAREAONLY
                          | Dwm.DWM_TNP_RECTSOURCE,
                opacity = 255,
                fVisible = true,
                fSourceClientAreaOnly = true,
                rcDestination = dest,
                rcSource = src,
            };
            Dwm.DwmUpdateThumbnailProperties(_thumbHandle, ref props);
        }
        catch
        {
        }
    }

    private void HideThumbnail()
    {
        if (_thumbHandle == IntPtr.Zero) return;
        try
        {
            var props = new Dwm.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = Dwm.DWM_TNP_VISIBLE,
                fVisible = false,
            };
            Dwm.DwmUpdateThumbnailProperties(_thumbHandle, ref props);
        }
        catch { }
    }
}
