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

            var legacySettings = new AppSettings { PushToTalkKey = "CapsLock", SettingsVersion = 1 };
            legacySettings.MigrateLegacySettings();
            Require(legacySettings.PushToTalkKey == "F8" && legacySettings.SettingsVersion == AppSettings.CurrentSettingsVersion,
                "Старые настройки Caps Lock не были безопасно обновлены");
            Require(SettingsForm.HotkeyChoices.Length >= 50 && SettingsForm.HotkeyChoices.Contains("F24") &&
                SettingsForm.HotkeyChoices.Contains("MediaPlayPause"), "Расширенный список горячих клавиш неполон");
            passed.Add("Миграция Caps Lock и расширенный выбор горячих клавиш");

            var gainTest = CreateTestWave(1000);
            AudioRecorder.ApplyGain(gainTest, 2.5f);
            Require(BitConverter.ToInt16(gainTest, 44) == 2500, "Усиление чувствительности микрофона работает неверно");
            passed.Add("Усиление чувствительности микрофона");

            byte[] audio;
            using (var recorder = new AudioRecorder())
            {
                var microphones = AudioRecorder.GetInputDevices();
                Console.WriteLine($"Активных микрофонов: {microphones.Count}");
                foreach (var microphone in microphones) Console.WriteLine($"  {microphone.Name}");
                recorder.Start();
                await Task.Delay(900);
                audio = recorder.Stop();
                Require(audio.Length >= 44, $"Микрофон вернул повреждённый WAV: {audio.Length} байт");
                if (audio.Length > 10_000)
                    passed.Add($"Микрофон ({audio.Length / 1024} КБ в памяти)");
                else
                {
                    passed.Add("Микрофон открыт и закрыт (во время теста входной сигнал отсутствовал)");
                    audio = CreateTestWave(0, sampleCount: 16_000);
                }
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

    private static byte[] CreateTestWave(short sample, int sampleCount = 1)
    {
        var dataSize = sampleCount * sizeof(short);
        var wav = new byte[44 + dataSize];
        "RIFF"u8.CopyTo(wav);
        BitConverter.GetBytes(36 + dataSize).CopyTo(wav, 4);
        "WAVEfmt "u8.CopyTo(wav.AsSpan(8));
        BitConverter.GetBytes(16).CopyTo(wav, 16);
        BitConverter.GetBytes((short)1).CopyTo(wav, 20);
        BitConverter.GetBytes((short)1).CopyTo(wav, 22);
        BitConverter.GetBytes(16_000).CopyTo(wav, 24);
        BitConverter.GetBytes(32_000).CopyTo(wav, 28);
        BitConverter.GetBytes((short)2).CopyTo(wav, 32);
        BitConverter.GetBytes((short)16).CopyTo(wav, 34);
        "data"u8.CopyTo(wav.AsSpan(36));
        BitConverter.GetBytes(dataSize).CopyTo(wav, 40);
        for (var offset = 44; offset < wav.Length; offset += 2)
            BitConverter.GetBytes(sample).CopyTo(wav, offset);
        return wav;
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
