using System.Windows;
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

    private IntPtr _thumbHandle = IntPtr.Zero;
    private IntPtr _destHwnd = IntPtr.Zero;
    private Window? _hostWindow;
    private bool _registered;

    public DwmThumbnailHost()
    {
        Focusable = false;
        IsHitTestVisible = true;
        Background = Brushes.Transparent;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdateThumbnailProperties();
        LayoutUpdated += (_, _) => UpdateThumbnailProperties();
    }

    public Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(Brush), typeof(DwmThumbnailHost),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

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
        {
            _hostWindow.SourceInitialized += HostOnSourceInitialized;
        }
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

    private void UpdateThumbnailProperties()
    {
        if (!_registered || _thumbHandle == IntPtr.Zero || _hostWindow == null) return;
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

            int left = (int)Math.Round(tl.X);
            int top = (int)Math.Round(tl.Y);
            int right = (int)Math.Round(br.X);
            int bottom = (int)Math.Round(br.Y);

            var dest = new Win32.RECT { Left = left, Top = top, Right = right, Bottom = bottom };

            // Compute source rect that preserves aspect ratio (letterbox)
            Win32.RECT srcRect = default;
            int flags = Dwm.DWM_TNP_VISIBLE | Dwm.DWM_TNP_RECTDESTINATION | Dwm.DWM_TNP_OPACITY | Dwm.DWM_TNP_SOURCECLIENTAREAONLY;

            int hr = Dwm.DwmQueryThumbnailSourceSize(_thumbHandle, out var size);
            if (hr == 0 && size.cx > 0 && size.cy > 0)
            {
                double destAspect = (double)(right - left) / Math.Max(1, bottom - top);
                double srcAspect = (double)size.cx / size.cy;
                if (Math.Abs(destAspect - srcAspect) > 0.01)
                {
                    // Letterbox the destination
                    int destW = right - left;
                    int destH = bottom - top;
                    if (destAspect > srcAspect)
                    {
                        int newW = (int)Math.Round(destH * srcAspect);
                        int offset = (destW - newW) / 2;
                        dest = new Win32.RECT { Left = left + offset, Top = top, Right = left + offset + newW, Bottom = bottom };
                    }
                    else
                    {
                        int newH = (int)Math.Round(destW / srcAspect);
                        int offset = (destH - newH) / 2;
                        dest = new Win32.RECT { Left = left, Top = top + offset, Right = right, Bottom = top + offset + newH };
                    }
                }
            }

            var props = new Dwm.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = flags,
                opacity = 255,
                fVisible = true,
                fSourceClientAreaOnly = true,
                rcDestination = dest,
                rcSource = srcRect,
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
