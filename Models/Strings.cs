namespace VoiceToText.Models;

internal static class Strings
{
    private static string _language = "ru";
    public static string Language => _language;
    public static void Use(string? language) => _language = language == "en" ? "en" : "ru";

    private static readonly Dictionary<string, (string Ru, string En)> Values = new()
    {
        ["Tagline"] = ("Локальный голосовой ввод для Windows", "Private voice typing for Windows"),
        ["Settings"] = ("Настройки", "Settings"),
        ["LastText"] = ("ПОСЛЕДНЯЯ РАСШИФРОВКА", "LATEST TRANSCRIPTION"),
        ["NoText"] = ("Здесь появится последняя распознанная фраза", "Your latest transcription will appear here"),
        ["Copy"] = ("Копировать", "Copy"),
        ["Copied"] = ("Скопировано", "Copied"),
        ["Privacy"] = ("Аудио обрабатывается локально и не сохраняется", "Audio is processed locally and never saved"),
        ["CancelHint"] = ("ESC — отменить запись или распознавание", "ESC — cancel recording or transcription"),
        ["Preparing"] = ("Подготовка…", "Preparing…"),
        ["LoadingModel"] = ("Загружаю модель…", "Loading model…"),
        ["DownloadingModel"] = ("Скачиваю модель…", "Downloading model…"),
        ["PreparingEngine"] = ("Подготавливаю движок…", "Preparing engine…"),
        ["Ready"] = ("Готов к работе", "Ready"),
        ["Listening"] = ("Слушаю…", "Listening…"),
        ["Transcribing"] = ("Распознаю…", "Transcribing…"),
        ["StartupFailed"] = ("Не удалось запустить", "Startup failed"),
        ["MicError"] = ("Ошибка микрофона", "Microphone error"),
        ["TranscriptionError"] = ("Ошибка распознавания", "Transcription error"),
        ["LocalOnly"] = ("всё обрабатывается локально", "everything stays on this computer"),
        ["FirstRun"] = ("первый запуск, затем интернет не понадобится", "first run only; no internet needed afterwards"),
        ["EngineStarting"] = ("модель загружена • запускаю локальное распознавание", "model downloaded • starting local recognition"),
        ["AlmostReady"] = ("Почти готово…", "Almost ready…"),
        ["SpeakRelease"] = ("Говорите — отпустите клавишу для распознавания", "Speak now — release the key to transcribe"),
        ["UsuallySeconds"] = ("Обычно это занимает несколько секунд", "This usually takes a few seconds"),
        ["LocalProcessing"] = ("Локальная обработка • ESC — отменить", "Processing locally • ESC to cancel"),
        ["TooShort"] = ("Запись слишком короткая", "Recording was too short"),
        ["NoSpeech"] = ("Речь не обнаружена", "No speech detected"),
        ["Cancelled"] = ("Отменено", "Cancelled"),
        ["Inserted"] = ("Вставлено", "Inserted"),
        ["Chars"] = ("символов", "characters"),
        ["AppReady"] = ("VoiceToText готов", "VoiceToText is ready"),
        ["HoldToDictate"] = ("Удерживайте {0}, чтобы диктовать.", "Hold {0} to dictate."),
        ["Background"] = ("VoiceToText работает в фоне", "VoiceToText is running in the background"),
        ["Show"] = ("Открыть VoiceToText", "Open VoiceToText"),
        ["Exit"] = ("Выйти", "Exit"),
        ["Model"] = ("Модель", "Model"),
        ["SpeechLanguage"] = ("Язык распознавания", "Recognition language"),
        ["InterfaceLanguage"] = ("Язык интерфейса", "Interface language"),
        ["Hotkey"] = ("Клавиша Push-to-Talk", "Push-to-Talk key"),
        ["Microphone"] = ("Микрофон", "Microphone"),
        ["Sensitivity"] = ("Чувствительность, %", "Sensitivity, %"),
        ["SystemDefault"] = ("Системный микрофон по умолчанию", "System default microphone"),
        ["Suppress"] = ("Не передавать PTT-клавишу другим приложениям (Caps Lock: короткое нажатие работает обычно)", "Do not pass the PTT key to other apps (Caps Lock: tap still toggles normally)"),
        ["RestoreClipboard"] = ("Восстанавливать буфер после вставки", "Restore clipboard after pasting"),
        ["CloseToTray"] = ("Закрывать окно в системный трей", "Close window to system tray"),
        ["StartMinimized"] = ("Запускать свёрнутым", "Start minimized"),
        ["SettingsHint"] = ("Настройте скорость, точность и управление", "Tune recognition quality and controls"),
        ["Save"] = ("Сохранить", "Save"),
        ["Cancel"] = ("Отмена", "Cancel"),
        ["Auto"] = ("Автоопределение", "Auto-detect"),
        ["Russian"] = ("Русский", "Russian"),
        ["English"] = ("Английский", "English"),
        ["DownloadRemainingMinutes"] = ("осталось ~{0} мин", "~{0} min left"),
        ["DownloadRemainingSeconds"] = ("осталось ~{0} с", "~{0} sec left"),
        ["Calculating"] = ("считаю время…", "estimating…"),
        ["Seconds"] = ("с", "sec")
    };

    public static string Get(string key)
    {
        var pair = Values[key];
        return _language == "en" ? pair.En : pair.Ru;
    }

    public static string Format(string key, params object[] args) => string.Format(Get(key), args);
}
