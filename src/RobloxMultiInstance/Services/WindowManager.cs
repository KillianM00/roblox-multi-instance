using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using RobloxMultiInstance.Models;

namespace RobloxMultiInstance.Services;

public static class WindowManager
{
    public static void Focus(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        if (Win32.IsIconic(hWnd)) Win32.ShowWindow(hWnd, Win32.SW_RESTORE);
        Win32.ForceForeground(hWnd);
    }

    public static void Minimize(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        Win32.ShowWindow(hWnd, Win32.SW_MINIMIZE);
    }

    public static (int x, int y, int cx, int cy)? GetRect(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !Win32.IsWindow(hWnd)) return null;
        if (!Win32.GetWindowRect(hWnd, out var r)) return null;
        return (r.Left, r.Top, r.Width, r.Height);
    }

    public static void MoveTo(IntPtr hWnd, int x, int y, int cx, int cy, bool activate = false)
    {
        if (hWnd == IntPtr.Zero) return;
        Win32.MoveWindowTo(hWnd, x, y, cx, cy, activate);
    }

    public static (int x, int y, int cx, int cy) ElementToScreenPixels(FrameworkElement element)
    {
        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        int x = (int)Math.Round(topLeft.X);
        int y = (int)Math.Round(topLeft.Y);
        int cx = (int)Math.Round(bottomRight.X - topLeft.X);
        int cy = (int)Math.Round(bottomRight.Y - topLeft.Y);
        return (x, y, cx, cy);
    }

    public static void EnterPlayMode(RobloxInstance inst, FrameworkElement playArea)
    {
        if (inst.Hwnd == IntPtr.Zero) return;
        if (inst.InPlayMode) return;

        var current = GetRect(inst.Hwnd);
        if (current.HasValue)
            inst.StorageRect = new RobloxInstance.Win32Rect(current.Value.x, current.Value.y, current.Value.cx, current.Value.cy);

        var (x, y, cx, cy) = ElementToScreenPixels(playArea);
        if (cx < 50 || cy < 50) return;

        if (Win32.IsIconic(inst.Hwnd)) Win32.ShowWindow(inst.Hwnd, Win32.SW_RESTORE);
        Win32.MoveWindowTo(inst.Hwnd, x, y, cx, cy, show: true);
        Win32.ForceForeground(inst.Hwnd);
        inst.InPlayMode = true;
    }

    public static void ExitPlayMode(RobloxInstance inst)
    {
        if (!inst.InPlayMode) return;
        if (inst.StorageRect != null && Win32.IsWindow(inst.Hwnd))
        {
            var r = inst.StorageRect;
            Win32.MoveWindowTo(inst.Hwnd, r.X, r.Y, r.Width, r.Height, show: false);
        }
        inst.InPlayMode = false;
    }

    public static void ArrangeOnPrimaryMonitor(IList<IntPtr> windows)
    {
        var live = windows.Where(h => h != IntPtr.Zero && Win32.IsWindow(h)).ToList();
        if (live.Count == 0) return;

        var area = SystemParameters.WorkArea;
        int cols = (int)Math.Ceiling(Math.Sqrt(live.Count));
        int rows = (int)Math.Ceiling((double)live.Count / cols);
        int cellW = (int)(area.Width / cols);
        int cellH = (int)(area.Height / rows);

        for (int i = 0; i < live.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            int x = (int)area.Left + col * cellW;
            int y = (int)area.Top + row * cellH;
            if (Win32.IsIconic(live[i])) Win32.ShowWindow(live[i], Win32.SW_RESTORE);
            Win32.MoveWindowTo(live[i], x, y, cellW, cellH, show: false);
        }
    }
}
