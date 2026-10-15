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
    private MMDevice? _device;
    private Exception? _recordingError;

    public event Action<float>? LevelChanged;
    public bool IsRecording { get; private set; }

    public static IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        return devices.Select(device => new AudioInputDevice(device.ID, device.FriendlyName)).ToArray();
    }

    public void Start(string? deviceId = null)
    {
        lock (_sync)
        {
            if (IsRecording) return;
            try
            {
                _audio = new MemoryStream();
                if (!string.IsNullOrWhiteSpace(deviceId))
                {
                    using var enumerator = new MMDeviceEnumerator();
                    _device = enumerator.GetDevice(deviceId);
                    if (_device.State != DeviceState.Active)
                        throw new InvalidOperationException("The selected microphone is not available.");
                    _capture = CreateCapture(_device);
                }
                else
                {
                    _capture = CreateCapture();
                }
                _stopped = new ManualResetEventSlim(false);
                _recordingError = null;
                _writer = new WaveFileWriter(_audio, _capture.WaveFormat);
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
                IsRecording = true;
                _capture.StartRecording();
            }
            catch
            {
                IsRecording = false;
                _capture?.Dispose();
                _device?.Dispose();
                _writer?.Dispose();
                _audio?.Dispose();
                _stopped?.Dispose();
                _capture = null;
                _device = null;
                _writer = null;
                _audio = null;
                _stopped = null;
                throw;
            }
        }
    }

    public byte[] Stop(int sensitivityPercent = 100) => StopCore(sensitivityPercent, convert: true);

    private static WasapiCapture CreateCapture(MMDevice? device = null)
    {
        // WasapiCapture posts RecordingStopped to the synchronization context it
        // sees in its constructor. StopCore waits for that event synchronously, so
        // capturing the WinForms context here would block the very UI thread that
        // must deliver the event and every stop would end in a false timeout.
        var context = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            return device is null ? new WasapiCapture() : new WasapiCapture(device);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    private byte[] StopCore(int sensitivityPercent, bool convert)
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

        try { capture.StopRecording(); }
        catch (Exception ex) { _recordingError ??= ex; }
        var stoppedCleanly = stopped?.Wait(TimeSpan.FromSeconds(3)) ?? true;
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
        Exception? cleanupError = null;
        try { writer.Dispose(); } catch (Exception ex) { cleanupError = ex; }
        try { capture.Dispose(); } catch (Exception ex) { cleanupError ??= ex; }
        try { _device?.Dispose(); } catch (Exception ex) { cleanupError ??= ex; }
        _device = null;
        stopped?.Dispose();

        if (!stoppedCleanly)
        {
            audio.Dispose();
            throw new TimeoutException("The microphone did not stop responding in time.");
        }
        if (_recordingError is { } recordingError)
        {
            audio.Dispose();
            throw new InvalidOperationException("Audio recording stopped unexpectedly.", recordingError);
        }
        if (cleanupError is not null)
        {
            audio.Dispose();
            throw new InvalidOperationException("The audio recording could not be finalized.", cleanupError);
        }
        if (!convert)
        {
            audio.Dispose();
            LevelChanged?.Invoke(0);
            return [];
        }

        audio.Position = 0;
        using var reader = new WaveFileReader(audio);
        using var resampler = new MediaFoundationResampler(reader, new WaveFormat(16_000, 16, 1))
        {
            ResamplerQuality = 60
        };
        using var converted = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(converted, resampler);
        var bytes = converted.ToArray();
        ApplyGain(bytes, Math.Clamp(sensitivityPercent, 100, 500) / 100f);
        audio.Dispose();
        LevelChanged?.Invoke(0);
        return bytes;
    }

    public void Cancel()
    {
        if (IsRecording) StopCore(100, convert: false);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_sync)
        {
            if (!IsRecording || _writer is null) return;
            _writer.Write(e.Buffer, 0, e.BytesRecorded);
        }

        var peak = 0f;
        var format = _capture?.WaveFormat;
        var isFloat = format?.Encoding == WaveFormatEncoding.IeeeFloat ||
            format is WaveFormatExtensible extensible && extensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT;
        if (isFloat && format?.BitsPerSample == 32)
        {
            for (var i = 0; i + 3 < e.BytesRecorded; i += 4)
            {
                var sample = Math.Abs(BitConverter.ToSingle(e.Buffer, i));
                if (sample > peak) peak = sample;
            }
        }
        else if (format?.BitsPerSample == 16)
        {
            for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
            {
                var sample = Math.Abs(BitConverter.ToInt16(e.Buffer, i) / 32768f);
                if (sample > peak) peak = sample;
            }
        }
        else if (!isFloat && format?.BitsPerSample == 24)
        {
            for (var i = 0; i + 2 < e.BytesRecorded; i += 3)
            {
                var value = e.Buffer[i] | e.Buffer[i + 1] << 8 | e.Buffer[i + 2] << 16;
                if ((value & 0x800000) != 0) value |= unchecked((int)0xff000000);
                var sample = Math.Abs(value / 8_388_608f);
                if (sample > peak) peak = sample;
            }
        }
        else if (!isFloat && format?.BitsPerSample == 32)
        {
            for (var i = 0; i + 3 < e.BytesRecorded; i += 4)
            {
                var sample = Math.Abs(BitConverter.ToInt32(e.Buffer, i) / 2_147_483_648f);
                if (sample > peak) peak = sample;
            }
        }
        LevelChanged?.Invoke(Math.Min(1f, peak * 2.5f));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _recordingError = e.Exception;
        _stopped?.Set();
    }

    internal static void ApplyGain(byte[] wav, float gain)
    {
        if (gain <= 1f || wav.Length < 44) return;
        var offset = FindDataChunk(wav);
        if (offset < 0) return;
        for (var i = offset; i + 1 < wav.Length; i += 2)
        {
            var sample = BitConverter.ToInt16(wav, i);
            var amplified = (int)Math.Round(sample * gain);
            amplified = Math.Clamp(amplified, short.MinValue, short.MaxValue);
            wav[i] = (byte)(amplified & 0xff);
            wav[i + 1] = (byte)((amplified >> 8) & 0xff);
        }
    }

    private static int FindDataChunk(byte[] wav)
    {
        var position = 12;
        while (position + 8 <= wav.Length)
        {
            var size = BitConverter.ToInt32(wav, position + 4);
            if (wav[position] == (byte)'d' && wav[position + 1] == (byte)'a' &&
                wav[position + 2] == (byte)'t' && wav[position + 3] == (byte)'a')
                return position + 8;
            if (size < 0 || position + 8L + size > wav.Length) return -1;
            position += 8 + size + (size & 1);
        }
        return -1;
    }

    public void Dispose()
    {
        try { if (IsRecording) Cancel(); } catch { }
        _capture?.Dispose();
        _device?.Dispose();
        _writer?.Dispose();
        _audio?.Dispose();
        _stopped?.Dispose();
    }
}

internal sealed record AudioInputDevice(string Id, string Name);
#pragma warning restore CS0618
