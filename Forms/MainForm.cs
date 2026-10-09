using System.Diagnostics;
using System.Runtime.InteropServices;
using VoiceToText.Controls;
using VoiceToText.Models;
using VoiceToText.Services;

namespace VoiceToText.Forms;

internal sealed class MainForm : Form
{
    private readonly AudioRecorder _recorder = new();
    private readonly WhisperService _whisper = new();
    private readonly GlobalKeyboardHook _keyboard;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly ToolStripMenuItem _showMenu = new();
    private readonly ToolStripMenuItem _settingsMenu = new();
    private readonly ToolStripMenuItem _exitMenu = new();

    private AppSettings _settings = AppSettings.Load();
    private readonly Label _tagline = new();
    private readonly Button _settingsButton = new();
    private readonly Label _statusDot = new();
    private readonly Label _status = new();
    private readonly Label _detail = new();
    private readonly Label _hotkeyHint = new();
    private readonly Label _cancelHint = new();
    private readonly Label _latestHeader = new();
    private readonly RichTextBox _latestText = new();
    private readonly Label _latestMeta = new();
    private readonly Button _copyButton = new();
    private readonly Label _privacy = new();
    private readonly Panel _meter = new();
    private readonly ModernProgressBar _progress = new();
    private readonly Label _progressText = new();

    private CancellationTokenSource? _operation;
    private CancellationTokenSource? _modelLoad;
    private string _latestValue = "";
    private bool _ready;
    private bool _busy;
    private bool _reallyClosing;
    private float _level;

    public MainForm()
    {
        Strings.Use(_settings.InterfaceLanguage);
        Text = "VoiceToText";
        ClientSize = new Size(720, 570);
        MinimumSize = new Size(640, 520);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Opacity = 0;
        BackColor = Color.FromArgb(13, 17, 24);
        ForeColor = Color.FromArgb(235, 239, 247);
        Font = new Font("Segoe UI", 10f);
        Icon = CreateAppIcon(Color.FromArgb(112, 99, 255));
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        BuildInterface();

        _keyboard = new GlobalKeyboardHook();
        ApplyHotkeySettings();
        _keyboard.Pressed += () => SafeUi(StartRecording);
        _keyboard.Released += () => SafeUi(async () => await StopAndTranscribeAsync());
        _keyboard.Cancelled += () => SafeUi(CancelCurrentOperation);
        _recorder.LevelChanged += value => { _level = value; SafeUi(_meter.Invalidate); };

        _showMenu.Click += (_, _) => ShowFromTray();
        _settingsMenu.Click += (_, _) => OpenSettings();
        _exitMenu.Click += (_, _) => ExitApplication();
        _trayMenu.Items.AddRange([_showMenu, _settingsMenu, new ToolStripSeparator(), _exitMenu]);
        _tray = new NotifyIcon { Icon = Icon, Visible = true, ContextMenuStrip = _trayMenu };
        _tray.DoubleClick += (_, _) => ShowFromTray();

        ApplyLanguage();
        RenderState(Strings.Get("LoadingModel"), $"{_settings.Model} • {Strings.Get("LocalOnly")}", Color.FromArgb(250, 183, 70), true);

        Shown += async (_, _) =>
        {
            PerformLayout();
            await Task.Delay(20);
            if (IsDisposed) return;
            if (_settings.StartMinimized) Hide();
            Opacity = 1;
            if (Environment.GetEnvironmentVariable("VOICETOTEXT_PREVIEW") == "1")
            {
                _ready = true;
                SetReadyState($"{_settings.Model} • {_settings.LanguageLabel()}");
                return;
            }
            await InitializeModelAsync();
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WsExComposited = 0x02000000;
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExComposited;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!OperatingSystem.IsWindows()) return;
        var enabled = 1;
        // Attribute 20 is current Windows 10/11; 19 covers older Windows 10 builds.
        if (DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(28, 24, 28, 20),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 186));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var brand = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = Padding.Empty };
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        brand.Controls.Add(new Label { Text = "VoiceToText", AutoSize = true, Font = new Font("Segoe UI Semibold", 22f), Margin = Padding.Empty }, 0, 0);
        _tagline.AutoSize = true;
        _tagline.ForeColor = Color.FromArgb(139, 150, 170);
        _tagline.Margin = new Padding(2, 1, 0, 0);
        brand.Controls.Add(_tagline, 0, 1);
        StyleButton(_settingsButton, 130, primary: false);
        _settingsButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _settingsButton.Margin = new Padding(12, 5, 0, 0);
        _settingsButton.Click += (_, _) => OpenSettings();
        header.Controls.Add(brand, 0, 0);
        header.Controls.Add(_settingsButton, 1, 0);
        root.Controls.Add(header, 0, 0);

        var stateCard = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 10) };
        var stateLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty };
        stateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        stateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        stateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        stateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
        stateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var statusRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty };
        _statusDot.Text = "●";
        _statusDot.AutoSize = true;
        _statusDot.Font = new Font("Segoe UI", 18f);
        _statusDot.Margin = new Padding(0, 1, 10, 0);
        _status.AutoSize = true;
        _status.Font = new Font("Segoe UI Semibold", 17f);
        _status.Margin = Padding.Empty;
        statusRow.Controls.AddRange([_statusDot, _status]);
        _detail.Dock = DockStyle.Fill;
        _detail.AutoEllipsis = true;
        _detail.ForeColor = Color.FromArgb(151, 161, 180);
        _detail.Margin = new Padding(2, 2, 0, 0);
        _meter.Dock = DockStyle.Fill;
        _meter.Margin = new Padding(2, 4, 2, 5);
        _meter.BackColor = Color.FromArgb(36, 42, 55);
        _meter.Paint += PaintMeter;
        _progress.Dock = DockStyle.Fill;
        _progress.Margin = new Padding(2, 1, 2, 1);
        _progress.IsIndeterminate = true;
        _progressText.Dock = DockStyle.Fill;
        _progressText.AutoEllipsis = true;
        _progressText.ForeColor = Color.FromArgb(151, 161, 180);
        _progressText.Font = new Font("Segoe UI", 9f);
        _progressText.Margin = new Padding(2, 6, 0, 0);
        stateLayout.Controls.Add(statusRow, 0, 0);
        stateLayout.Controls.Add(_detail, 0, 1);
        stateLayout.Controls.Add(_meter, 0, 2);
        stateLayout.Controls.Add(_progress, 0, 3);
        stateLayout.Controls.Add(_progressText, 0, 4);
        stateCard.Controls.Add(stateLayout);
        root.Controls.Add(stateCard, 0, 1);

        var hintCard = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 10), CardColor = Color.FromArgb(18, 23, 33), Padding = new Padding(22, 14, 22, 12) };
        var hintLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = Padding.Empty };
        hintLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        hintLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        _hotkeyHint.AutoSize = true;
        _hotkeyHint.Font = new Font("Segoe UI Semibold", 12f);
        _hotkeyHint.Margin = new Padding(0, 2, 0, 0);
        _cancelHint.AutoSize = true;
        _cancelHint.ForeColor = Color.FromArgb(135, 146, 166);
        _cancelHint.Margin = Padding.Empty;
        hintLayout.Controls.Add(_hotkeyHint, 0, 0);
        hintLayout.Controls.Add(_cancelHint, 0, 1);
        hintCard.Controls.Add(hintLayout);
        root.Controls.Add(hintCard, 0, 2);

        _latestHeader.Dock = DockStyle.Fill;
        _latestHeader.TextAlign = ContentAlignment.MiddleLeft;
        _latestHeader.ForeColor = Color.FromArgb(119, 131, 152);
        _latestHeader.Font = new Font("Segoe UI Semibold", 8.5f);
        _latestHeader.Margin = new Padding(3, 0, 0, 0);
        root.Controls.Add(_latestHeader, 0, 3);

        var latestCard = new CardPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(20, 16, 16, 14) };
        var latestLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        latestLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        latestLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        latestLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        latestLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        _latestText.Dock = DockStyle.Fill;
        _latestText.ReadOnly = true;
        _latestText.BorderStyle = BorderStyle.None;
        _latestText.BackColor = latestCard.CardColor;
        _latestText.ForeColor = Color.FromArgb(126, 137, 157);
        _latestText.Font = new Font("Segoe UI", 12f);
        _latestText.ScrollBars = RichTextBoxScrollBars.Vertical;
        _latestText.Margin = new Padding(2, 2, 8, 4);
        _latestMeta.Dock = DockStyle.Fill;
        _latestMeta.TextAlign = ContentAlignment.MiddleLeft;
        _latestMeta.ForeColor = Color.FromArgb(114, 126, 147);
        _latestMeta.Font = new Font("Segoe UI", 8.5f);
        _latestMeta.Margin = new Padding(2, 0, 0, 0);
        StyleButton(_copyButton, 112, primary: false);
        _copyButton.Dock = DockStyle.Fill;
        _copyButton.Margin = new Padding(8, 3, 0, 1);
        _copyButton.Enabled = false;
        _copyButton.Click += (_, _) => CopyLatest();
        latestLayout.Controls.Add(_latestText, 0, 0);
        latestLayout.SetColumnSpan(_latestText, 2);
        latestLayout.Controls.Add(_latestMeta, 0, 1);
        latestLayout.Controls.Add(_copyButton, 1, 1);
        latestCard.Controls.Add(latestLayout);
        root.Controls.Add(latestCard, 0, 4);

        _privacy.Dock = DockStyle.Fill;
        _privacy.TextAlign = ContentAlignment.MiddleCenter;
        _privacy.ForeColor = Color.FromArgb(102, 114, 134);
        _privacy.Font = new Font("Segoe UI", 8.5f);
        _privacy.Margin = new Padding(0, 7, 0, 0);
        root.Controls.Add(_privacy, 0, 5);
    }

    private async Task InitializeModelAsync()
    {
        _modelLoad?.Cancel();
        _modelLoad?.Dispose();
        _modelLoad = new CancellationTokenSource();
        var currentLoad = _modelLoad;
        _ready = false;
        RenderState(Strings.Get("LoadingModel"), $"{_settings.Model} • {Strings.Get("LocalOnly")}", Color.FromArgb(250, 183, 70), true);
        try
        {
            var progress = new Progress<ModelLoadProgress>(value =>
            {
                if (!ReferenceEquals(_modelLoad, currentLoad)) return;
                if (value.Stage == ModelLoadStage.Preparing)
                {
                    RenderState(Strings.Get("PreparingEngine"), $"{_settings.Model} • {Strings.Get("EngineStarting")}", Color.FromArgb(250, 183, 70), true);
                    _progress.IsIndeterminate = true;
                    _progressText.Text = Strings.Get("AlmostReady");
                    return;
                }
                RenderState(Strings.Get("DownloadingModel"), $"{_settings.Model} • {Strings.Get("FirstRun")}", Color.FromArgb(250, 183, 70), true);
                _progress.IsIndeterminate = false;
                _progress.Value = value.Percent;
                _progressText.Text = FormatDownloadProgress(value);
            });
            await _whisper.InitializeAsync(_settings, progress, currentLoad.Token);
            if (!ReferenceEquals(_modelLoad, currentLoad)) return;
            _ready = true;
            SetReadyState($"{_settings.Model} • {_settings.LanguageLabel()}");
            ShowToast(Strings.Get("AppReady"), Strings.Format("HoldToDictate", _settings.PushToTalkKey));
        }
        catch (OperationCanceledException) when (currentLoad.IsCancellationRequested) { }
        catch (Exception ex) { RenderState(Strings.Get("StartupFailed"), FriendlyError(ex), Color.FromArgb(255, 103, 121)); }
        finally
        {
            if (ReferenceEquals(_modelLoad, currentLoad))
            {
                currentLoad.Dispose();
                _modelLoad = null;
            }
        }
    }

    private void StartRecording()
    {
        if (!_ready || _busy) return;
        try
        {
            _operation = new CancellationTokenSource();
            _recorder.Start(_settings.MicrophoneId);
            _busy = true;
            RenderState(Strings.Get("Listening"), Strings.Get("SpeakRelease"), Color.FromArgb(255, 92, 118));
        }
        catch (Exception ex)
        {
            _busy = false;
            RenderState(Strings.Get("MicError"), FriendlyError(ex), Color.FromArgb(255, 103, 121));
        }
    }

    private async Task StopAndTranscribeAsync()
    {
        if (!_busy || !_recorder.IsRecording) return;
        byte[] audio;
        try
        {
            audio = _recorder.Stop(_settings.MicrophoneSensitivity);
        }
        catch (Exception ex)
        {
            _busy = false;
            RenderState(Strings.Get("MicError"), FriendlyError(ex), Color.FromArgb(255, 103, 121));
            return;
        }
        if (audio.Length < 10_000)
        {
            _busy = false;
            SetReadyState(Strings.Get("TooShort"));
            return;
        }

        RenderState(Strings.Get("Transcribing"), Strings.Get("UsuallySeconds"), Color.FromArgb(113, 132, 255), true);
        _progressText.Text = Strings.Get("LocalProcessing");
        var watch = Stopwatch.StartNew();
        try
        {
            var text = await _whisper.TranscribeAsync(audio, _operation?.Token ?? CancellationToken.None);
            if (string.IsNullOrWhiteSpace(text))
            {
                SetReadyState(Strings.Get("NoSpeech"));
                return;
            }

            ShowLatest(text, watch.Elapsed);
            _keyboard.Enabled = false;
            await TextInserter.PasteAsync(text, _settings.RestoreClipboard);
            _keyboard.Enabled = true;
            SetReadyState($"{Strings.Get("Inserted")}: {text.Length} {Strings.Get("Chars")} • {watch.Elapsed.TotalSeconds:0.0} {Strings.Get("Seconds")}");
        }
        catch (OperationCanceledException) { SetReadyState(Strings.Get("Cancelled")); }
        catch (Exception ex) { RenderState(Strings.Get("TranscriptionError"), FriendlyError(ex), Color.FromArgb(255, 103, 121)); }
        finally
        {
            _keyboard.Enabled = true;
            _busy = false;
            _operation?.Dispose();
            _operation = null;
        }
    }

    private void CancelCurrentOperation()
    {
        if (!_busy) return;
        _operation?.Cancel();
        try
        {
            if (_recorder.IsRecording) _recorder.Cancel();
        }
        catch (Exception ex)
        {
            _busy = false;
            RenderState(Strings.Get("MicError"), FriendlyError(ex), Color.FromArgb(255, 103, 121));
            return;
        }
        _busy = false;
        SetReadyState(Strings.Get("Cancelled"));
    }

    private void ShowLatest(string text, TimeSpan duration)
    {
        _latestValue = text;
        _latestText.Text = text;
        _latestText.ForeColor = Color.FromArgb(232, 236, 245);
        _latestMeta.Text = $"{DateTime.Now:HH:mm:ss}  •  {duration.TotalSeconds:0.0} {Strings.Get("Seconds")}";
        _copyButton.BackColor = Color.FromArgb(105, 91, 255);
        _copyButton.Enabled = true;
    }

    private void CopyLatest()
    {
        if (string.IsNullOrWhiteSpace(_latestValue)) return;
        Clipboard.SetText(_latestValue);
        SetReadyState(Strings.Get("Copied"));
    }

    private void OpenSettings()
    {
        if (_busy) return;
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var reload = dialog.Result.Model != _settings.Model || dialog.Result.Language != _settings.Language;
        _settings = dialog.Result;
        Strings.Use(_settings.InterfaceLanguage);
        ApplyLanguage();
        ApplyHotkeySettings();
        if (reload) _ = InitializeModelAsync();
        else SetReadyState($"{_settings.Model} • {_settings.LanguageLabel()}");
    }

    private void ApplyLanguage()
    {
        _tagline.Text = Strings.Get("Tagline");
        _settingsButton.Text = "⚙  " + Strings.Get("Settings");
        _hotkeyHint.Text = Strings.Format("HoldToDictate", _settings.PushToTalkKey.ToUpperInvariant());
        _cancelHint.Text = Strings.Get("CancelHint");
        _latestHeader.Text = Strings.Get("LastText");
        _copyButton.Text = Strings.Get("Copy");
        _privacy.Text = Strings.Get("Privacy");
        _showMenu.Text = Strings.Get("Show");
        _settingsMenu.Text = Strings.Get("Settings");
        _exitMenu.Text = Strings.Get("Exit");
        if (string.IsNullOrEmpty(_latestValue))
        {
            _latestText.Text = Strings.Get("NoText");
            _latestText.ForeColor = Color.FromArgb(126, 137, 157);
        }
        if (_tray is not null) _tray.Text = "VoiceToText";
    }

    private void ApplyHotkeySettings()
    {
        _keyboard.PushToTalkKey = Enum.TryParse<Keys>(_settings.PushToTalkKey, true, out var key) ? key : Keys.F8;
        _keyboard.Suppress = _settings.SuppressPushToTalkKey;
    }

    private void SetReadyState(string detail) => RenderState(Strings.Get("Ready"), detail, Color.FromArgb(64, 211, 144));

    private void RenderState(string title, string detail, Color color, bool showProgress = false)
    {
        _status.Text = title;
        _detail.Text = detail;
        _statusDot.ForeColor = color;
        Text = $"VoiceToText — {title}";
        _progress.Visible = showProgress;
        _progressText.Visible = showProgress;
        if (showProgress)
        {
            _progress.IsIndeterminate = true;
            _progressText.Text = Strings.Get("Preparing");
        }
        else _progress.Value = 0;
        if (_tray is not null)
        {
            _tray.Text = ($"VoiceToText — {title}").Truncate(63);
            _tray.Icon = CreateAppIcon(color);
        }
        _meter.Invalidate();
    }

    private void PaintMeter(object? sender, PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(36, 42, 55));
        var width = _recorder.IsRecording ? (int)(_meter.Width * Math.Max(.03f, _level)) : (_ready ? 5 : 0);
        using var brush = new SolidBrush(_recorder.IsRecording ? Color.FromArgb(255, 92, 118) : Color.FromArgb(112, 99, 255));
        e.Graphics.FillRectangle(brush, 0, 0, width, _meter.Height);
    }

    private static void StyleButton(Button button, int width, bool primary)
    {
        button.Width = width;
        button.Height = 40;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = primary ? Color.FromArgb(105, 91, 255) : Color.FromArgb(31, 37, 50);
        button.ForeColor = Color.White;
        button.Cursor = Cursors.Hand;
        button.Font = new Font("Segoe UI Semibold", 9.5f);
    }

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

    private static string FriendlyError(Exception ex) => ex.GetBaseException().Message.Truncate(150);

    private static string FormatDownloadProgress(ModelLoadProgress value)
    {
        static string Size(long bytes) => bytes >= 1_000_000_000 ? $"{bytes / 1_000_000_000d:0.00} GB" : $"{bytes / 1_000_000d:0} MB";
        var speed = value.BytesPerSecond >= 1_000_000 ? $"{value.BytesPerSecond / 1_000_000d:0.0} MB/s" : $"{value.BytesPerSecond / 1_000d:0} KB/s";
        var eta = value.Remaining is { } remaining
            ? remaining.TotalMinutes >= 1
                ? Strings.Format("DownloadRemainingMinutes", Math.Ceiling(remaining.TotalMinutes))
                : Strings.Format("DownloadRemainingSeconds", Math.Max(1, Math.Ceiling(remaining.TotalSeconds)))
            : Strings.Get("Calculating");
        return $"{value.Percent}%  •  {Size(value.DownloadedBytes)} / {Size(value.TotalBytes)}  •  {speed}  •  {eta}";
    }

    private void SafeUi(Action action)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(action); else action();
    }

    private void ShowFromTray() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    private void ShowToast(string title, string text) { if (_tray.Visible) _tray.ShowBalloonTip(2500, title, text, ToolTipIcon.Info); }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_reallyClosing && _settings.CloseToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            ShowToast(Strings.Get("Background"), Strings.Format("HoldToDictate", _settings.PushToTalkKey));
            return;
        }
        base.OnFormClosing(e);
    }

    private void ExitApplication() { _reallyClosing = true; Close(); }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _modelLoad?.Cancel();
            _modelLoad?.Dispose();
            _operation?.Cancel();
            _keyboard?.Dispose();
            _recorder.Dispose();
            _whisper.Dispose();
            if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        }
        base.Dispose(disposing);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);
}

internal static class UiExtensions
{
    public static string Truncate(this string value, int length) => value.Length <= length ? value : value[..length];
    public static string LanguageLabel(this AppSettings settings) => settings.Language switch
    {
        "ru" => Strings.Get("Russian"),
        "en" => Strings.Get("English"),
        _ => Strings.Get("Auto")
    };
}
