using System.Runtime.InteropServices;

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
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyInput(ushort key, bool up) => new()
    {
        Type = 1,
        Data = new INPUTUNION { Keyboard = new KEYBDINPUT { VirtualKey = key, Flags = up ? 2u : 0u } }
    };

    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint Type; public INPUTUNION Data; }
    [StructLayout(LayoutKind.Explicit)] private struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort VirtualKey; public ushort ScanCode; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
}
