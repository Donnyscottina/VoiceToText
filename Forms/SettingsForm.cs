using VoiceToText.Controls;
using VoiceToText.Models;

namespace VoiceToText.Forms;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox _model = NewCombo();
    private readonly ComboBox _language = NewCombo();
    private readonly ComboBox _interfaceLanguage = NewCombo();
    private readonly ComboBox _hotkey = NewCombo();
    private readonly CheckBox _suppress = NewCheck();
    private readonly CheckBox _startMinimized = NewCheck();
    private readonly CheckBox _closeToTray = NewCheck();
    private readonly CheckBox _restoreClipboard = NewCheck();

    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        Result = settings;
        Strings.Use(settings.InterfaceLanguage);
        Text = $"{Strings.Get("Settings")} — VoiceToText";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(550, 610);
        MinimumSize = new Size(500, 570);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(13, 17, 24);
        ForeColor = Color.FromArgb(235, 239, 247);
        Font = new Font("Segoe UI", 10f);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(28, 24, 28, 22), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        Controls.Add(root);

        root.Controls.Add(new Label { Text = Strings.Get("Settings"), AutoSize = true, Font = new Font("Segoe UI Semibold", 22f), Margin = Padding.Empty }, 0, 0);
        root.Controls.Add(new Label { Text = Strings.Get("SettingsHint"), AutoSize = true, ForeColor = Color.FromArgb(139, 150, 170), Margin = new Padding(2, 0, 0, 8) }, 0, 1);

        var card = new CardPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(22, 18, 22, 18) };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8, Margin = Padding.Empty };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        for (var i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        for (var i = 4; i < 8; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        card.Controls.Add(grid);
        root.Controls.Add(card, 0, 2);

        _model.Items.AddRange(["tiny", "base", "small", "medium", "large-v3-turbo", "large-v3"]);
        _model.SelectedItem = settings.Model;
        if (_model.SelectedIndex < 0) _model.SelectedItem = "large-v3";
        _language.Items.AddRange([Strings.Get("Auto"), Strings.Get("Russian"), Strings.Get("English")]);
        _language.SelectedIndex = settings.Language switch { "ru" => 1, "en" => 2, _ => 0 };
        _interfaceLanguage.Items.AddRange(["Русский", "English"]);
        _interfaceLanguage.SelectedIndex = settings.InterfaceLanguage == "en" ? 1 : 0;
        _hotkey.Items.AddRange(["CapsLock", "F8", "F9", "F10", "F11", "F12"]);
        _hotkey.SelectedItem = settings.PushToTalkKey;
        if (_hotkey.SelectedIndex < 0) _hotkey.SelectedItem = "CapsLock";

        AddField(grid, 0, Strings.Get("Model"), _model);
        AddField(grid, 1, Strings.Get("SpeechLanguage"), _language);
        AddField(grid, 2, Strings.Get("InterfaceLanguage"), _interfaceLanguage);
        AddField(grid, 3, Strings.Get("Hotkey"), _hotkey);

        ConfigureCheck(_suppress, Strings.Get("Suppress"), settings.SuppressPushToTalkKey);
        ConfigureCheck(_restoreClipboard, Strings.Get("RestoreClipboard"), settings.RestoreClipboard);
        ConfigureCheck(_closeToTray, Strings.Get("CloseToTray"), settings.CloseToTray);
        ConfigureCheck(_startMinimized, Strings.Get("StartMinimized"), settings.StartMinimized);
        grid.Controls.Add(_suppress, 0, 4); grid.SetColumnSpan(_suppress, 2);
        grid.Controls.Add(_restoreClipboard, 0, 5); grid.SetColumnSpan(_restoreClipboard, 2);
        grid.Controls.Add(_closeToTray, 0, 6); grid.SetColumnSpan(_closeToTray, 2);
        grid.Controls.Add(_startMinimized, 0, 7); grid.SetColumnSpan(_startMinimized, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 12, 0, 0), Margin = Padding.Empty };
        var save = NewButton(Strings.Get("Save"), true);
        var cancel = NewButton(Strings.Get("Cancel"), false);
        save.Click += (_, _) => SaveAndClose();
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.AddRange([save, cancel]);
        root.Controls.Add(buttons, 0, 3);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static ComboBox NewCombo() => new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(39, 46, 61),
        ForeColor = Color.White,
        Margin = new Padding(8, 7, 0, 7)
    };

    private static CheckBox NewCheck() => new() { Dock = DockStyle.Fill, AutoSize = true, ForeColor = Color.FromArgb(196, 203, 216), Margin = new Padding(0, 4, 0, 0) };

    private static void AddField(TableLayoutPanel grid, int row, string text, Control input)
    {
        grid.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(196, 203, 216), Margin = Padding.Empty }, 0, row);
        grid.Controls.Add(input, 1, row);
    }

    private static void ConfigureCheck(CheckBox box, string text, bool value) { box.Text = text; box.Checked = value; }

    private static Button NewButton(string text, bool primary) => new()
    {
        Text = text,
        Size = new Size(112, 40),
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Color.FromArgb(105, 91, 255) : Color.FromArgb(31, 37, 50),
        ForeColor = Color.White,
        Cursor = Cursors.Hand,
        Font = new Font("Segoe UI Semibold", 9.5f),
        Margin = new Padding(8, 0, 0, 0),
        FlatAppearance = { BorderSize = 0 }
    };

    private void SaveAndClose()
    {
        Result = new AppSettings
        {
            Model = _model.SelectedItem?.ToString() ?? "large-v3",
            Language = _language.SelectedIndex switch { 1 => "ru", 2 => "en", _ => "auto" },
            InterfaceLanguage = _interfaceLanguage.SelectedIndex == 1 ? "en" : "ru",
            PushToTalkKey = _hotkey.SelectedItem?.ToString() ?? "CapsLock",
            SuppressPushToTalkKey = _suppress.Checked,
            StartMinimized = _startMinimized.Checked,
            CloseToTray = _closeToTray.Checked,
            RestoreClipboard = _restoreClipboard.Checked
        };
        Result.Save();
        DialogResult = DialogResult.OK;
    }
}
