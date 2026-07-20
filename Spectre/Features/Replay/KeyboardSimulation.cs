using System.Runtime.InteropServices;

namespace Spectre.Features.Replay;

internal static class KeyboardSimulation
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetKeyboardState(byte[] pbKeyState);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private const int KEYEVENTF_KEYDOWN = 0;
    private const int KEYEVENTF_KEYUP = 2;
    private const int KEYEVENTF_EXTENDEDKEY = 1;

    public static void PressKey(byte keyCode, bool isExtended = false)
    {
        int flags = isExtended ? KEYEVENTF_EXTENDEDKEY : 0;
        keybd_event(keyCode, 0, flags, 0);
    }

    public static void ReleaseKey(byte keyCode, bool isExtended = false)
    {
        int flags = KEYEVENTF_KEYUP | (isExtended ? KEYEVENTF_EXTENDEDKEY : 0);
        keybd_event(keyCode, 0, flags, 0);
    }

    public static void SendKey(byte keyCode, bool press, bool isExtended = false)
    {
        if (press) PressKey(keyCode, isExtended);
        else ReleaseKey(keyCode, isExtended);
    }
}
