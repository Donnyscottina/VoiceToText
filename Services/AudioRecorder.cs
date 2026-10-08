using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoiceToText.Services;

#pragma warning disable CS0618 // WasapiCapture remains the stable event-based compatibility API in NAudio 3.
internal sealed class AudioRecorder : IDisposable
{
    private readonly object _sync = new();
    private WasapiCapture? _capture;
    private MemoryStream? _audio;
    private WaveFileWriter? _writer;
    private ManualResetEventSlim? _stopped;

    public event Action<float>? LevelChanged;
    public bool IsRecording { get; private set; }

    public void Start()
    {
        lock (_sync)
        {
            if (IsRecording) return;
            try
            {
                _audio = new MemoryStream();
                _capture = new WasapiCapture();
                _stopped = new ManualResetEventSlim(false);
                _writer = new WaveFileWriter(_audio, _capture.WaveFormat);
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
                _capture.StartRecording();
                IsRecording = true;
            }
            catch
            {
                _capture?.Dispose();
                _writer?.Dispose();
                _audio?.Dispose();
                _stopped?.Dispose();
                _capture = null;
                _writer = null;
                _audio = null;
                _stopped = null;
                throw;
            }
        }
    }

    public byte[] Stop()
    {
        WasapiCapture? capture;
        WaveFileWriter? writer;
        MemoryStream? audio;
        ManualResetEventSlim? stopped;
        lock (_sync)
        {
            if (!IsRecording || _capture is null || _writer is null || _audio is null)
                return [];
            capture = _capture;
            writer = _writer;
            audio = _audio;
            stopped = _stopped;
        }

        capture.StopRecording();
        stopped?.Wait(TimeSpan.FromSeconds(2));
        lock (_sync)
        {
            IsRecording = false;
            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
            _capture = null;
            _writer = null;
            _audio = null;
            _stopped = null;
        }
        writer.Flush();
        writer.Dispose();
        capture.Dispose();
        stopped?.Dispose();

        audio.Position = 0;
        using var reader = new WaveFileReader(audio);
        using var resampler = new MediaFoundationResampler(reader, new WaveFormat(16_000, 16, 1))
        {
            ResamplerQuality = 60
        };
        using var converted = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(converted, resampler);
        var bytes = converted.ToArray();
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
        var format = _capture?.WaveFormat;
        if (format?.BitsPerSample == 32)
        {
            for (var i = 0; i + 3 < e.BytesRecorded; i += 4)
            {
                var sample = Math.Abs(BitConverter.ToSingle(e.Buffer, i));
                if (sample > peak) peak = sample;
            }
        }
        else
        {
            for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
            {
                var sample = Math.Abs(BitConverter.ToInt16(e.Buffer, i) / 32768f);
                if (sample > peak) peak = sample;
            }
        }
        LevelChanged?.Invoke(Math.Min(1f, peak * 2.5f));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e) => _stopped?.Set();

    public void Dispose()
    {
        if (IsRecording) Cancel();
        _capture?.Dispose();
        _writer?.Dispose();
        _audio?.Dispose();
        _stopped?.Dispose();
    }
}
#pragma warning restore CS0618
