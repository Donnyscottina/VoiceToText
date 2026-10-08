using System.Text;
using VoiceToText.Models;
using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceToText.Services;

internal sealed class WhisperService : IDisposable
{
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public static string ModelsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoiceToText", "models");

    public async Task InitializeAsync(AppSettings settings, IProgress<int>? progress = null, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            DisposeEngine();
            Directory.CreateDirectory(ModelsDirectory);
            var (type, fileName) = ResolveModel(settings.Model);
            var path = Path.Combine(ModelsDirectory, fileName);
            if (!File.Exists(path)) await DownloadModelAsync(type, path, progress, token);

            _factory = WhisperFactory.FromPath(path);
            var language = settings.Language == "auto" ? "auto" : settings.Language;
            _processor = _factory.CreateBuilder().WithLanguage(language).Build();
        }
        finally { _gate.Release(); }
    }

    public async Task<string> TranscribeAsync(byte[] wav, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (_processor is null) throw new InvalidOperationException("Модель ещё не загружена.");
            var result = new StringBuilder();
            await using var stream = new MemoryStream(wav, writable: false);
            await foreach (var segment in _processor.ProcessAsync(stream, token))
                result.Append(segment.Text.Trim()).Append(' ');
            return result.ToString().Trim();
        }
        finally { _gate.Release(); }
    }

    private static async Task DownloadModelAsync(GgmlType type, string path, IProgress<int>? progress, CancellationToken token)
    {
        var temporaryPath = path + ".download";
        try
        {
            using var source = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(type, cancellationToken: token);
            await using var target = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), token);
                total += read;
                progress?.Report((int)Math.Min(95, total / 2_000_000));
            }
            await target.FlushAsync(token);
            File.Move(temporaryPath, path, true);
            progress?.Report(100);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    private static (GgmlType Type, string FileName) ResolveModel(string model) => model switch
    {
        "tiny" => (GgmlType.Tiny, "ggml-tiny.bin"),
        "base" => (GgmlType.Base, "ggml-base.bin"),
        "medium" => (GgmlType.Medium, "ggml-medium.bin"),
        "large-v3" => (GgmlType.LargeV3, "ggml-large-v3.bin"),
        "large-v3-turbo" => (GgmlType.LargeV3Turbo, "ggml-large-v3-turbo.bin"),
        _ => (GgmlType.Small, "ggml-small.bin")
    };

    private void DisposeEngine()
    {
        _processor?.Dispose();
        _factory?.Dispose();
        _processor = null;
        _factory = null;
    }

    public void Dispose()
    {
        DisposeEngine();
        _gate.Dispose();
    }
}
