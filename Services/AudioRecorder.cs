using NAudio.Wave;

namespace VoiceToText.Services;

internal sealed class AudioRecorder : IDisposable
{
    private readonly object _sync = new();
    private WaveIn? _capture;
    private MemoryStream? _audio;
    private WaveFileWriter? _writer;

    public event Action<float>? LevelChanged;
    public bool IsRecording { get; private set; }

    public void Start()
    {
        lock (_sync)
        {
            if (IsRecording) return;
            _audio = new MemoryStream();
            _capture = new WaveIn
            {
                WaveFormat = new WaveFormat(16_000, 16, 1),
                BufferMilliseconds = 50,
                NumberOfBuffers = 3
            };
            _writer = new WaveFileWriter(_audio, _capture.WaveFormat);
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
            IsRecording = true;
        }
    }

    public byte[] Stop()
    {
        WaveIn? capture;
        WaveFileWriter? writer;
        MemoryStream? audio;
        lock (_sync)
        {
            if (!IsRecording || _capture is null || _writer is null || _audio is null)
                return [];
            IsRecording = false;
            capture = _capture;
            writer = _writer;
            audio = _audio;
            _capture = null;
            _writer = null;
            _audio = null;
        }

        capture.DataAvailable -= OnDataAvailable;
        capture.StopRecording();
        writer.Flush();
        writer.Dispose();
        var bytes = audio.ToArray();
        capture.Dispose();
        audio.Dispose();
        LevelChanged?.Invoke(0);
        return bytes;
    }

    public void Cancel() => Stop();

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_sync)
        {
            if (!IsRecording || _writer is null) return;
            _writer.Write(e.Buffer, 0, e.BytesRecorded);
        }

        var peak = 0f;
        for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
        {
            var sample = Math.Abs(BitConverter.ToInt16(e.Buffer, i) / 32768f);
            if (sample > peak) peak = sample;
        }
        LevelChanged?.Invoke(Math.Min(1f, peak * 2.5f));
    }

    public void Dispose()
    {
        if (IsRecording) Cancel();
        _capture?.Dispose();
        _writer?.Dispose();
        _audio?.Dispose();
    }
}
