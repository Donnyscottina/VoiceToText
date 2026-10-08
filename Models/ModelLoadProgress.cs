namespace VoiceToText.Models;

internal enum ModelLoadStage
{
    Downloading,
    Preparing
}

internal sealed record ModelLoadProgress(
    ModelLoadStage Stage,
    long DownloadedBytes = 0,
    long TotalBytes = 0,
    int Percent = 0,
    double BytesPerSecond = 0,
    TimeSpan? Remaining = null);
