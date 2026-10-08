using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoiceToText.Services;

internal sealed class GlobalKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private readonly HookProc _callback;
    private IntPtr _hook;
    private bool _keyIsDown;

    public Keys PushToTalkKey { get; set; } = Keys.CapsLock;
    public bool Suppress { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public event Action? Pressed;
    public event Action? Released;
    public event Action? Cancelled;

    public GlobalKeyboardHook()
    {
        _callback = Callback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(module.ModuleName), 0);
        if (_hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
    }

    private IntPtr Callback(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || !Enabled) return CallNextHookEx(_hook, code, message, data);
        var key = (Keys)Marshal.ReadInt32(data);
        var down = message == (IntPtr)WmKeyDown || message == (IntPtr)WmSysKeyDown;
        var up = message == (IntPtr)WmKeyUp || message == (IntPtr)WmSysKeyUp;

        if (key == Keys.Escape && down)
        {
            Cancelled?.Invoke();
            return CallNextHookEx(_hook, code, message, data);
        }

        if (key != PushToTalkKey) return CallNextHookEx(_hook, code, message, data);
        if (down && !_keyIsDown)
        {
            _keyIsDown = true;
            Pressed?.Invoke();
        }
        else if (up && _keyIsDown)
        {
            _keyIsDown = false;
            Released?.Invoke();
        }
        return Suppress ? (IntPtr)1 : CallNextHookEx(_hook, code, message, data);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? moduleName);
}
