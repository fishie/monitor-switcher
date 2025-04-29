using System.Runtime.InteropServices;

namespace MonitorSwitcherGui;

public static class SendMessageApi
{
    public const int HWND_BROADCAST = 0xFFFF;
    public const int WM_SYSCOMMAND   = 0x0112;
    public const int SC_MONITORPOWER = 0xf170;

    public const int MONITOR_ON = -1;
    public const int MONITOR_OFF = 2;
    public const int MONITOR_STANBY = 1;

    [DllImport("user32")]
    public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
}
