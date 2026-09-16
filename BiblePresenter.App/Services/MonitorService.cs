using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Screen = System.Windows.Forms.Screen;

namespace BiblePresenter.App.Services;

/// <summary>
/// Enumerates connected displays and positions the output window full-screen on a chosen
/// one, using SetWindowPos with physical pixel coordinates so it's correct regardless of
/// WPF's device-independent-pixel scaling.
/// </summary>
public sealed class MonitorService
{
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    public IReadOnlyList<Screen> GetScreens() => Screen.AllScreens;

    public Screen? GetProjectorScreen()
        => Screen.AllScreens.FirstOrDefault(s => !s.Primary) ?? Screen.AllScreens.FirstOrDefault();

    public void PositionFullScreenOn(Window window, Screen screen)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var bounds = screen.Bounds;
        SetWindowPos(hwnd, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoZOrder | SwpNoActivate);
    }
}
