using System.Runtime.InteropServices;
using System.ComponentModel;

namespace VoiceToText.Services;

internal static class TextInserter
{
    public static async Task PasteAsync(string text, bool restoreClipboard)
    {
        string? previous = null;
        if (restoreClipboard)
        {
            try { if (Clipboard.ContainsText()) previous = Clipboard.GetText(); } catch { }
        }

        Clipboard.SetText(text);
        await Task.Delay(60);
        SendCtrlV();

        if (restoreClipboard && previous is not null)
        {
            await Task.Delay(700);
            try { Clipboard.SetText(previous); } catch { }
        }
    }

    private static void SendCtrlV()
    {
        INPUT[] inputs =
        [
            KeyInput(0x11, false), KeyInput(0x56, false),
            KeyInput(0x56, true), KeyInput(0x11, true)
        ];
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows не разрешила вставить текст в активное окно.");
    }

    private static INPUT KeyInput(ushort key, bool up) => new()
    {
        Type = 1,
        Data = new INPUTUNION { Keyboard = new KEYBDINPUT { VirtualKey = key, Flags = up ? 2u : 0u } }
    };

    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint Type; public INPUTUNION Data; }
    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT Mouse;
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
        [FieldOffset(0)] public HARDWAREINPUT Hardware;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int X; public int Y; public uint MouseData; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort VirtualKey; public ushort ScanCode; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct HARDWAREINPUT { public uint Message; public ushort ParamL; public ushort ParamH; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
}
