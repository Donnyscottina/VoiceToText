using System.Diagnostics;
using VoiceToText.Models;
using VoiceToText.Services;

namespace VoiceToText.Forms;

internal sealed class MainForm : Form
{
    private readonly AppSettings _initialSettings = AppSettings.Load();
    private AppSettings _settings;
    private readonly AudioRecorder _recorder = new();
    private readonly WhisperService _whisper = new();
    private readonly GlobalKeyboardHook _keyboard;
    private readonly NotifyIcon _tray;
    private readonly Label _status = new();
    private readonly Label _detail = new();
    private readonly Label _hotkeyHint = new();
    private readonly Panel _statusDot = new();
    private readonly Panel _meter = new();
    private readonly FlowLayoutPanel _history = new();
    private readonly ProgressBar _progress = new();
    private CancellationTokenSource? _operation;
    private bool _ready;
    private bool _busy;
    private bool _reallyClosing;
    private float _level;

    public MainForm()
    {
        _settings = _initialSettings;
        Text = "VoiceToText";
        ClientSize = new Size(760, 620);
        MinimumSize = new Size(680, 540);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(13, 17, 24);
        ForeColor = Color.FromArgb(235, 239, 247);
        Font = new Font("Segoe UI", 10f);
        Icon = CreateAppIcon(Color.FromArgb(100, 93, 255));

        BuildInterface();
        _keyboard = new GlobalKeyboardHook();
        ApplyHotkeySettings();
        _keyboard.Pressed += () => SafeUi(StartRecording);
        _keyboard.Released += () => SafeUi(async () => await StopAndTranscribeAsync());
        _keyboard.Cancelled += () => SafeUi(CancelCurrentOperation);
        _recorder.LevelChanged += value => { _level = value; SafeUi(_meter.Invalidate); };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть VoiceToText", null, (_, _) => ShowFromTray());
        menu.Items.Add("Настройки", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выйти", null, (_, _) => ExitApplication());
        _tray = new NotifyIcon
        {
            Icon = Icon, Text = "VoiceToText — запуск", Visible = true, ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => ShowFromTray();

        Shown += async (_, _) =>
        {
            if (_settings.StartMinimized) Hide();
            if (Environment.GetEnvironmentVariable("VOICETOTEXT_PREVIEW") == "1")
            {
                _ready = true;
                SetReadyState($"Модель {_settings.Model} • {_settings.LanguageLabel()}");
                return;
            }
            await InitializeModelAsync();
        };
    }

    private void BuildInterface()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 88, Padding = new Padding(28, 20, 28, 12) };
        var brand = new Label { Text = "VoiceToText", Font = new Font("Segoe UI Semibold", 22f), AutoSize = true, Location = new Point(28, 18) };
        var tagline = new Label { Text = "Локальный голосовой ввод для Windows", AutoSize = true, ForeColor = Color.FromArgb(138, 148, 166), Location = new Point(31, 57) };
        var settingsButton = MakeButton("⚙  Настройки", 126);
        settingsButton.Dock = DockStyle.Right;
        settingsButton.Margin = new Padding(0, 7, 0, 7);
        settingsButton.Click += (_, _) => OpenSettings();
        header.Controls.AddRange([brand, tagline, settingsButton]);
        Controls.Add(header);

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 8, 28, 20) };
        Controls.Add(content);
        header.BringToFront();

        var stateCard = new Panel { Dock = DockStyle.Top, Height = 152, BackColor = Color.FromArgb(24, 29, 40), Padding = new Padding(24) };
        _statusDot.Size = new Size(16, 16); _statusDot.Location = new Point(25, 29); _statusDot.BackColor = Color.FromArgb(134, 142, 157);
        _status.Text = "Подготовка…"; _status.AutoSize = true; _status.Font = new Font("Segoe UI Semibold", 17f); _status.Location = new Point(52, 21);
        _detail.Text = "Проверяю модель распознавания"; _detail.AutoSize = true; _detail.ForeColor = Color.FromArgb(145, 154, 172); _detail.Location = new Point(27, 62);
        _meter.Location = new Point(27, 97); _meter.Size = new Size(650, 24); _meter.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top; _meter.BackColor = Color.FromArgb(35, 41, 54); _meter.Paint += PaintMeter;
        _progress.Location = new Point(27, 128); _progress.Size = new Size(650, 5); _progress.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top; _progress.Style = ProgressBarStyle.Marquee;
        stateCard.Controls.AddRange([_statusDot, _status, _detail, _meter, _progress]);
        content.Controls.Add(stateCard);

        var hintCard = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.FromArgb(18, 23, 33), Padding = new Padding(22), Margin = new Padding(0, 14, 0, 0) };
        _hotkeyHint.AutoSize = true; _hotkeyHint.Font = new Font("Segoe UI Semibold", 12f); _hotkeyHint.Location = new Point(22, 16);
        var escapeHint = new Label { Text = "ESC — отменить запись или распознавание", AutoSize = true, ForeColor = Color.FromArgb(135, 145, 164), Location = new Point(24, 47) };
        hintCard.Controls.AddRange([_hotkeyHint, escapeHint]);
        content.Controls.Add(hintCard);
        hintCard.BringToFront();

        var historyHeader = new Label { Text = "ИСТОРИЯ", Dock = DockStyle.Top, Height = 42, Padding = new Padding(2, 20, 0, 0), ForeColor = Color.FromArgb(119, 130, 150), Font = new Font("Segoe UI Semibold", 9f) };
        content.Controls.Add(historyHeader); historyHeader.BringToFront();
        _history.Dock = DockStyle.Fill; _history.FlowDirection = FlowDirection.TopDown; _history.WrapContents = false; _history.AutoScroll = true; _history.BackColor = BackColor; _history.Padding = new Padding(0, 8, 0, 0);
        content.Controls.Add(_history); _history.BringToFront();

        UpdateHotkeyHint();
    }

    private async Task InitializeModelAsync()
    {
        SetState("Загружаю модель…", $"{_settings.Model} • всё обрабатывается локально", Color.FromArgb(250, 183, 70), true);
        _ready = false;
        try
        {
            var progress = new Progress<int>(value => { _progress.Style = ProgressBarStyle.Continuous; _progress.Value = value; _detail.Text = $"Скачивание модели {_settings.Model}: {value}%"; });
            await _whisper.InitializeAsync(_settings, progress);
            _ready = true;
            SetState("Готов к работе", $"Модель {_settings.Model} • {_settings.LanguageLabel()}", Color.FromArgb(64, 211, 144));
            ShowToast("VoiceToText готов", $"Удерживайте {_settings.PushToTalkKey}, чтобы диктовать.");
        }
        catch (Exception ex)
        {
            SetState("Не удалось запустить", FriendlyError(ex), Color.FromArgb(255, 103, 121));
        }
    }

    private void StartRecording()
    {
        if (!_ready || _busy) return;
        try
        {
            _operation = new CancellationTokenSource();
            _recorder.Start();
            _busy = true;
            SetState("Слушаю…", "Говорите — отпустите клавишу для распознавания", Color.FromArgb(255, 92, 118));
        }
        catch (Exception ex) { _busy = false; SetState("Ошибка микрофона", FriendlyError(ex), Color.FromArgb(255, 103, 121)); }
    }

    private async Task StopAndTranscribeAsync()
    {
        if (!_busy || !_recorder.IsRecording) return;
        var audio = _recorder.Stop();
        if (audio.Length < 10_000)
        {
            _busy = false;
            SetReadyState("Запись слишком короткая");
            return;
        }

        SetState("Распознаю…", "Обычно это занимает несколько секунд", Color.FromArgb(113, 132, 255), true);
        var watch = Stopwatch.StartNew();
        try
        {
            var text = await _whisper.TranscribeAsync(audio, _operation?.Token ?? CancellationToken.None);
            if (string.IsNullOrWhiteSpace(text))
            {
                SetReadyState("Речь не обнаружена");
                return;
            }
            AddHistory(new TranscriptionItem(DateTime.Now, text, watch.Elapsed));
            _keyboard.Enabled = false;
            await TextInserter.PasteAsync(text, _settings.RestoreClipboard);
            _keyboard.Enabled = true;
            SetReadyState($"Вставлено: {text.Length} символов • {watch.Elapsed.TotalSeconds:0.0} с");
        }
        catch (OperationCanceledException) { SetReadyState("Отменено"); }
        catch (Exception ex) { SetState("Ошибка распознавания", FriendlyError(ex), Color.FromArgb(255, 103, 121)); }
        finally { _keyboard.Enabled = true; _busy = false; _operation?.Dispose(); _operation = null; }
    }

    private void CancelCurrentOperation()
    {
        if (!_busy) return;
        _operation?.Cancel();
        if (_recorder.IsRecording) _recorder.Cancel();
        _busy = false;
        SetReadyState("Отменено");
    }

    private void AddHistory(TranscriptionItem item)
    {
        var card = new Panel { Width = Math.Max(580, _history.ClientSize.Width - 28), Height = 86, BackColor = Color.FromArgb(24, 29, 40), Margin = new Padding(0, 0, 0, 8), Padding = new Padding(16) };
        var text = new Label { Text = item.Text, AutoEllipsis = true, MaximumSize = new Size(card.Width - 32, 44), AutoSize = true, ForeColor = Color.FromArgb(230, 235, 245), Location = new Point(16, 13) };
        var meta = new Label { Text = $"{item.Time:HH:mm:ss}  •  {item.Duration.TotalSeconds:0.0} с", AutoSize = true, ForeColor = Color.FromArgb(114, 125, 145), Location = new Point(16, 61), Font = new Font("Segoe UI", 8.5f) };
        card.Controls.AddRange([text, meta]);
        _history.Controls.Add(card);
        _history.Controls.SetChildIndex(card, 0);
        while (_history.Controls.Count > 8) _history.Controls.RemoveAt(_history.Controls.Count - 1);
    }

    private void OpenSettings()
    {
        if (_busy) return;
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var reload = dialog.Result.Model != _settings.Model || dialog.Result.Language != _settings.Language;
        _settings = dialog.Result;
        ApplyHotkeySettings();
        UpdateHotkeyHint();
        if (reload) _ = InitializeModelAsync();
    }

    private void ApplyHotkeySettings()
    {
        _keyboard.PushToTalkKey = Enum.TryParse<Keys>(_settings.PushToTalkKey, true, out var key) ? key : Keys.CapsLock;
        _keyboard.Suppress = _settings.SuppressPushToTalkKey;
    }

    private void UpdateHotkeyHint() => _hotkeyHint.Text = $"Удерживайте  {_settings.PushToTalkKey.ToUpperInvariant()}  и говорите";

    private void SetReadyState(string detail) => SetState("Готов к работе", detail, Color.FromArgb(64, 211, 144));

    private void SetState(string title, string detail, Color color, bool progress = false)
    {
        _status.Text = title; _detail.Text = detail; _statusDot.BackColor = color;
        _progress.Visible = progress; if (!progress) _progress.Value = 0;
        if (_tray is not null) _tray.Text = ("VoiceToText — " + title).Truncate(63);
        _meter.Invalidate();
    }

    private void PaintMeter(object? sender, PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(35, 41, 54));
        var width = _recorder.IsRecording ? (int)(_meter.Width * Math.Max(.03f, _level)) : (_ready ? 5 : 0);
        using var brush = new SolidBrush(_recorder.IsRecording ? Color.FromArgb(255, 92, 118) : Color.FromArgb(100, 93, 255));
        e.Graphics.FillRectangle(brush, 0, 0, width, _meter.Height);
    }

    private static Button MakeButton(string text, int width) => new()
    {
        Text = text, Size = new Size(width, 38), FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(31, 37, 50), ForeColor = Color.FromArgb(220, 225, 235),
        Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }
    };

    private static Icon CreateAppIcon(Color color)
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 2, 2, 60, 60);
        using var font = new Font("Segoe UI", 27, FontStyle.Bold);
        TextRenderer.DrawText(graphics, "V", font, new Rectangle(0, 4, 64, 56), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void SafeUi(Action action)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(action); else action();
    }

    private void ShowFromTray() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    private void ShowToast(string title, string text) { if (_tray.Visible) _tray.ShowBalloonTip(2500, title, text, ToolTipIcon.Info); }
    private static string FriendlyError(Exception ex) => ex.GetBaseException().Message.Truncate(150);

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_reallyClosing && _settings.CloseToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true; Hide(); ShowToast("VoiceToText работает в фоне", $"Удерживайте {_settings.PushToTalkKey}, чтобы диктовать."); return;
        }
        base.OnFormClosing(e);
    }

    private void ExitApplication() { _reallyClosing = true; Close(); }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _operation?.Cancel(); _keyboard?.Dispose(); _recorder.Dispose(); _whisper.Dispose();
            if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        }
        base.Dispose(disposing);
    }
}

internal static class UiExtensions
{
    public static string Truncate(this string value, int length) => value.Length <= length ? value : value[..length];
    public static string LanguageLabel(this AppSettings settings) => settings.Language switch { "ru" => "русский", "en" => "English", _ => "автоопределение языка" };
}
