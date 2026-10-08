using System.Diagnostics;
using VoiceToText.Forms;
using VoiceToText.Models;
using VoiceToText.Services;

namespace VoiceToText.SmokeTest;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main()
    {
        var passed = new List<string>();
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Environment.SetEnvironmentVariable("VOICETOTEXT_PREVIEW", "1");

            using (var mainForm = new MainForm())
            {
                mainForm.Show();
                Application.DoEvents();
                Require(mainForm.Visible && mainForm.Text.StartsWith("VoiceToText", StringComparison.Ordinal) && mainForm.ClientSize.Width >= 680,
                    "Главное окно не прошло проверку");
                mainForm.Hide();
                passed.Add("UI и глобальный keyboard hook");
            }

            foreach (var uiLanguage in new[] { "ru", "en" })
            {
                var settings = new AppSettings { InterfaceLanguage = uiLanguage };
                using var settingsForm = new SettingsForm(settings);
                settingsForm.Show();
                Application.DoEvents();
                Require(settingsForm.Visible && settingsForm.ClientSize.Width >= 500 && settingsForm.ClientSize.Height >= 570,
                    $"Окно настроек ({uiLanguage}) не прошло проверку размеров");
                Require(AllChildrenFit(settingsForm), $"Элементы настроек ({uiLanguage}) выходят за границы окна");
                settingsForm.Hide();
            }
            passed.Add("Русский и английский интерфейс");

            byte[] audio;
            using (var recorder = new AudioRecorder())
            {
                recorder.Start();
                await Task.Delay(900);
                audio = recorder.Stop();
                Require(audio.Length > 10_000, "Микрофон вернул слишком мало аудиоданных");
                passed.Add($"Микрофон ({audio.Length / 1024} КБ в памяти)");
            }

            using (var whisper = new WhisperService())
            using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                var settings = new AppSettings { Model = "tiny", Language = "ru" };
                var lastProgress = -1;
                var download = new Progress<ModelLoadProgress>(value =>
                {
                    if (value.Stage != ModelLoadStage.Downloading || value.Percent <= Volatile.Read(ref lastProgress)) return;
                    Interlocked.Exchange(ref lastProgress, value.Percent);
                    Console.Write($"\rЗагрузка тестовой модели: {value.Percent,3}%");
                });
                var watch = Stopwatch.StartNew();
                await whisper.InitializeAsync(settings, download, timeout.Token);
                var text = await whisper.TranscribeAsync(audio, timeout.Token);
                Console.WriteLine();
                passed.Add($"Whisper tiny ({watch.Elapsed.TotalSeconds:0.0} с, результат: {text.Length} симв.)");
            }

            await TestInsertionAsync();
            passed.Add("Unicode-вставка и восстановление буфера");

            Console.WriteLine("\nЛокальная проверка пройдена:");
            foreach (var item in passed) Console.WriteLine($"  ✓ {item}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nSmoke-test не пройден: {ex.GetBaseException().Message}");
            return 1;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool AllChildrenFit(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible) continue;
            if (child.Left < 0 || child.Top < 0 || child.Right > parent.ClientSize.Width + 1 || child.Bottom > parent.ClientSize.Height + 1)
                return false;
            if (!AllChildrenFit(child)) return false;
        }
        return true;
    }

    private static Task TestInsertionAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var clipboardMarker = $"VoiceToText проверка {Guid.NewGuid():N}";
            var originalClipboard = $"Буфер до проверки {Guid.NewGuid():N}";
            using var target = new Form
            {
                Text = "VoiceToText insertion test",
                Size = new Size(500, 160),
                StartPosition = FormStartPosition.CenterScreen
            };
            using var field = new TextBox { Dock = DockStyle.Fill, Multiline = true };
            target.Controls.Add(field);
            target.Shown += async (_, _) =>
            {
                try
                {
                    Clipboard.SetText(originalClipboard);
                    target.Activate();
                    field.Focus();
                    await Task.Delay(150);
                    await TextInserter.PasteAsync(clipboardMarker, restoreClipboard: true);
                    Require(Clipboard.GetText() == originalClipboard, "Буфер обмена не был восстановлен");
                    completion.SetResult();
                }
                catch (Exception ex) { completion.SetException(ex); }
                finally { target.Close(); }
            };
            Application.Run(target);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return completion.Task;
    }
}
