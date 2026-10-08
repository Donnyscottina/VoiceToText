# Участие в разработке / Contributing

Я разрабатываю и поддерживаю VoiceToText один. Если вы нашли ошибку или хотите предложить функцию, откройте issue. Pull request лучше заранее согласовать со мной в issue, чтобы работа не пропала зря.

I develop and maintain VoiceToText on my own. If you find a bug or want to suggest a feature, please open an issue. Please discuss a pull request with me in an issue first, so your work does not go to waste.

## Локальная проверка / Local verification

```powershell
dotnet restore
dotnet build VoiceToText.slnx -c Release
```

Перед pull request / Before a pull request:

1. Убедитесь, что сборка проходит без предупреждений. / Make sure the build has no warnings.
2. Проверьте запись, `Esc`, Unicode-вставку и трей. / Test recording, `Esc`, Unicode insertion, and the tray.
3. Не добавляйте `*.bin`, настройки и `artifacts/`. / Do not commit `*.bin`, settings, or `artifacts/`.
4. Обновите оба README при изменении поведения. / Update both README files when behavior changes.

Я предпочитаю небольшие понятные коммиты. Для ошибки приложите шаги воспроизведения, ожидаемое и фактическое поведение.

I prefer small, focused commits. For a bug, include reproduction steps plus expected and actual behavior.
