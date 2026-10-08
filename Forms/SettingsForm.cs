using VoiceToText.Models;

namespace VoiceToText.Forms;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox _model = new();
    private readonly ComboBox _language = new();
    private readonly ComboBox _hotkey = new();
    private readonly CheckBox _suppress = new();
    private readonly CheckBox _startMinimized = new();
    private readonly CheckBox _closeToTray = new();
    private readonly CheckBox _restoreClipboard = new();

    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        Result = settings;
        Text = "Настройки — VoiceToText";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(500, 555);
        BackColor = Color.FromArgb(18, 22, 31);
        ForeColor = Color.FromArgb(231, 235, 243);
        Font = new Font("Segoe UI", 10f);

        var title = new Label
        {
            Text = "Настройки",
            Font = new Font("Segoe UI Semibold", 20f),
            AutoSize = true,
            Location = new Point(28, 24)
        };
        var subtitle = new Label
        {
            Text = "Подберите баланс скорости и точности под свой компьютер",
            ForeColor = Color.FromArgb(144, 153, 170), AutoSize = true, Location = new Point(31, 65)
        };
        Controls.AddRange([title, subtitle]);

        var panel = new Panel { Location = new Point(28, 105), Size = new Size(444, 360), BackColor = Color.FromArgb(27, 33, 45) };
        Controls.Add(panel);

        ConfigureCombo(_model, ["tiny", "base", "small", "medium", "large-v3", "large-v3-turbo"]);
        _model.SelectedItem = settings.Model;
        ConfigureCombo(_language, ["Автоопределение", "Русский", "English"]);
        _language.SelectedIndex = settings.Language switch { "ru" => 1, "en" => 2, _ => 0 };
        ConfigureCombo(_hotkey, ["CapsLock", "F8", "F9", "F10", "F11", "F12"]);
        _hotkey.SelectedItem = settings.PushToTalkKey;

        AddRow(panel, "Модель", _model, 20);
        AddRow(panel, "Язык", _language, 88);
        AddRow(panel, "Клавиша Push-to-Talk", _hotkey, 156);

        ConfigureCheck(_suppress, "Блокировать клавишу в других приложениях", settings.SuppressPushToTalkKey, 224);
        ConfigureCheck(_restoreClipboard, "Восстанавливать буфер обмена после вставки", settings.RestoreClipboard, 254);
        ConfigureCheck(_closeToTray, "Закрывать окно в системный трей", settings.CloseToTray, 284);
        ConfigureCheck(_startMinimized, "Запускать свёрнутым", settings.StartMinimized, 314);
        panel.Controls.AddRange([_suppress, _restoreClipboard, _closeToTray, _startMinimized]);

        var cancel = MakeButton("Отмена", Color.FromArgb(44, 51, 66));
        cancel.Location = new Point(276, 495);
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        var save = MakeButton("Сохранить", Color.FromArgb(100, 93, 255));
        save.Location = new Point(374, 495);
        save.Click += (_, _) => SaveAndClose();
        Controls.AddRange([cancel, save]);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static void ConfigureCombo(ComboBox combo, string[] values)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = Color.FromArgb(39, 46, 61);
        combo.ForeColor = Color.White;
        combo.Items.AddRange(values);
        combo.Width = 205;
        combo.Height = 32;
    }

    private static void AddRow(Control panel, string label, Control input, int y)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(20, y + 8), ForeColor = Color.FromArgb(190, 198, 213) });
        input.Location = new Point(216, y);
        panel.Controls.Add(input);
    }

    private static void ConfigureCheck(CheckBox box, string text, bool value, int y)
    {
        box.Text = text;
        box.Checked = value;
        box.AutoSize = true;
        box.Location = new Point(20, y);
        box.ForeColor = Color.FromArgb(190, 198, 213);
    }

    private static Button MakeButton(string text, Color color) => new()
    {
        Text = text, Size = new Size(98, 38), FlatStyle = FlatStyle.Flat,
        BackColor = color, ForeColor = Color.White, Cursor = Cursors.Hand,
        FlatAppearance = { BorderSize = 0 }
    };

    private void SaveAndClose()
    {
        Result = new AppSettings
        {
            Model = _model.SelectedItem?.ToString() ?? "small",
            Language = _language.SelectedIndex switch { 1 => "ru", 2 => "en", _ => "auto" },
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
