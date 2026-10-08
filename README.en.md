# VoiceToText

**English** · [Русский](README.md)

I built VoiceToText as a fast, private voice-typing app for Windows: hold a hotkey, speak, and the recognized text appears in the active application.

The app is written in C# and recognizes speech entirely on your computer. I do not send recordings to the cloud, collect telemetry, or save audio. Recognition is powered by [Whisper](https://github.com/openai/whisper) through [Whisper.net](https://github.com/sandrohanea/whisper.net).

## What I built

- global Push-to-Talk in any application;
- Russian and English user interfaces;
- Russian, English, and automatic spoken-language detection;
- local transcription and Unicode insertion into the active field;
- Vulkan acceleration with automatic CPU fallback;
- models ranging from fast `tiny` to highly accurate `large-v3`;
- visible model-download progress with percentage, size, speed, and ETA;
- `Esc` cancellation, microphone level, and system tray support;
- the latest transcription with a dedicated copy button;
- an adaptive dark UI that stays responsive while a model is loading.

## Quick start

1. Open the [latest release](https://github.com/Donnyscottina/VoiceToText/releases/latest).
2. Download and extract `VoiceToText-win-x64.zip`.
3. Run `VoiceToText.exe`.
4. Wait for the first model download. I selected `large-v3` by default; it is about 3.1 GB.
5. Focus any text field, hold `Caps Lock`, speak, and release the key.

Press `Esc` to cancel recording or transcription. Closing the window keeps the app running in the system tray.

> Windows may show SmartScreen because the build is not code-signed. If you downloaded it from this repository's Releases page, choose **More info → Run anyway**.

## Settings

The app lets you choose:

- model: `tiny`, `base`, `small`, `medium`, `large-v3-turbo`, or `large-v3`;
- recognition language: auto-detect, Russian, or English;
- interface language: English or Russian;
- Push-to-Talk key and key suppression;
- clipboard restoration after insertion;
- start-minimized and close-to-tray behavior.

Settings are stored in `%APPDATA%\VoiceToText\settings.json`; models are stored in `%LOCALAPPDATA%\VoiceToText\models`.

## Choosing a model

| Model | Speed | Accuracy | Approx. size |
|---|---:|---:|---:|
| `tiny` | fastest | basic | 75 MB |
| `base` | fast | fair | 142 MB |
| `small` | balanced | good | 466 MB |
| `medium` | slower | very good | 1.5 GB |
| `large-v3-turbo` | medium | excellent | 1.6 GB |
| `large-v3` | slowest | highest | 3.1 GB |

I use `large-v3` as the default. If speed or memory matters more, I recommend `small`; `large-v3-turbo` is a strong compromise for a capable GPU.

## Run from source

You need Windows 10/11 and the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
git clone https://github.com/Donnyscottina/VoiceToText.git
cd VoiceToText
dotnet restore
dotnet run --project VoiceToText.csproj
```

## Build a release

```powershell
.\scripts\publish.ps1
```

The script creates a self-contained build in `artifacts\VoiceToText-win-x64`. Users do not need a separate .NET installation.

## Troubleshooting

**The hotkey does not work in an elevated window.** Windows prevents a regular process from controlling an elevated one. Run VoiceToText as administrator only when this is required.

**Recognition is slow.** Select a smaller model. Whisper.net automatically uses Vulkan when available and falls back to the CPU otherwise.

**Text is not inserted.** Verify that normal `Ctrl+V` works in the target field. VoiceToText temporarily uses the clipboard and restores it after about 700 ms.

**The model will not download.** Check your connection and free disk space. VoiceToText removes an incomplete download and retries on the next launch.

## Development

```powershell
dotnet restore
dotnet build VoiceToText.slnx -c Release
dotnet run --project tests/VoiceToText.SmokeTest/VoiceToText.SmokeTest.csproj -c Release
```

I keep the codebase compact: `Forms/` contains the UI, `Services/` contains audio, Whisper, hotkeys, and insertion, while `Models/` contains settings and localization. GitHub Actions builds the Windows artifact automatically.

## Author

I am [Donnyscottina](https://github.com/Donnyscottina), the sole author and developer of VoiceToText.

## License

I release VoiceToText under the [MIT License](LICENSE). Dependencies retain their respective licenses.
