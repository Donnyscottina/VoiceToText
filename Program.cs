using VoiceToText.Forms;

namespace VoiceToText;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(true, "Local\\VoiceToText.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("VoiceToText уже запущен — проверьте системный трей.", "VoiceToText",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
