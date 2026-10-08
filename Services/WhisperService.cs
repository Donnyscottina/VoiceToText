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

    public async Task InitializeAsync(AppSettings settings, IProgress<ModelLoadProgress>? progress = null, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            await Task.Run(DisposeEngine, token);
            Directory.CreateDirectory(ModelsDirectory);
            var (type, fileName, expectedBytes) = ResolveModel(settings.Model);
            var path = Path.Combine(ModelsDirectory, fileName);
            if (!File.Exists(path)) await DownloadModelAsync(type, path, expectedBytes, progress, token);

            progress?.Report(new ModelLoadProgress(ModelLoadStage.Preparing));
            var language = settings.Language == "auto" ? "auto" : settings.Language;
            var engine = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var factory = WhisperFactory.FromPath(path);
                try
                {
                    var processor = factory.CreateBuilder().WithLanguage(language).Build();
                    return (Factory: factory, Processor: processor);
                }
                catch
                {
                    factory.Dispose();
                    throw;
                }
            }, token);
            _factory = engine.Factory;
            _processor = engine.Processor;
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

    private static async Task DownloadModelAsync(GgmlType type, string path, long expectedBytes, IProgress<ModelLoadProgress>? progress, CancellationToken token)
    {
        var temporaryPath = path + ".download";
        try
        {
            // A killed process may leave an incomplete download behind.
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            using var source = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(type, cancellationToken: token);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var lastPercent = -1;
            await using (var target = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), token);
                    total += read;
                    var percent = (int)Math.Min(99, total * 100 / expectedBytes);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        var speed = total / Math.Max(0.1, watch.Elapsed.TotalSeconds);
                        var secondsLeft = speed > 0 ? Math.Max(0, expectedBytes - total) / speed : 0;
                        progress?.Report(new ModelLoadProgress(
                            ModelLoadStage.Downloading,
                            total,
                            expectedBytes,
                            percent,
                            speed,
                            TimeSpan.FromSeconds(secondsLeft)));
                    }
                }
                await target.FlushAsync(token);
            }
            File.Move(temporaryPath, path, true);
            progress?.Report(new ModelLoadProgress(
                ModelLoadStage.Downloading,
                expectedBytes,
                expectedBytes,
                100,
                expectedBytes / Math.Max(0.1, watch.Elapsed.TotalSeconds),
                TimeSpan.Zero));
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    private static (GgmlType Type, string FileName, long ExpectedBytes) ResolveModel(string model) => model switch
    {
        "tiny" => (GgmlType.Tiny, "ggml-tiny.bin", 78_000_000),
        "base" => (GgmlType.Base, "ggml-base.bin", 148_000_000),
        "medium" => (GgmlType.Medium, "ggml-medium.bin", 1_534_000_000),
        "large-v3" => (GgmlType.LargeV3, "ggml-large-v3.bin", 3_095_000_000),
        "large-v3-turbo" => (GgmlType.LargeV3Turbo, "ggml-large-v3-turbo.bin", 1_625_000_000),
        _ => (GgmlType.Small, "ggml-small.bin", 488_000_000)
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
