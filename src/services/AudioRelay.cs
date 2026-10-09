using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VBAudioMonitor;

/// <summary>
/// Manages real-time audio loopback capture and low-latency playback relay.
/// </summary>
internal sealed class AudioRelay : IDisposable
{
    private MMDevice? inputDevice;
    private MMDevice? outputDevice;
    private WasapiLoopbackCapture? loopbackCapture;
    private WasapiOut? playbackOut;
    private SmoothGainProvider? volumeProvider;
    private BufferedWaveProvider? sampleQueue;
    private long totalBytesRecorded;
    private int bufferUnderrunEvents;
    private int selectedChannelOffset;

    public event Action<Exception>? Failed;

    public WaveFormat? SourceFormat => loopbackCapture?.WaveFormat;
    public bool SourceMuted => inputDevice?.AudioEndpointVolume.Mute == true;
    public bool OutputMuted => outputDevice?.AudioEndpointVolume.Mute == true;
    public float InputPeak => inputDevice?.AudioMeterInformation.MasterPeakValue ?? 0f;
    public long BytesCaptured => Interlocked.Read(ref totalBytesRecorded);
    public int UnderrunCount => Volatile.Read(ref bufferUnderrunEvents);
    public double BufferedMilliseconds => sampleQueue?.BufferedDuration.TotalMilliseconds ?? 0;
    public bool DevicesAvailable => inputDevice?.State == DeviceState.Active && outputDevice?.State == DeviceState.Active;

    /// <summary>
    /// Gets peak value for the left stereo channel.
    /// </summary>
    public float GetLeftPeak()
    {
        if (inputDevice == null) return 0f;
        try
        {
            var channelPeaks = inputDevice.AudioMeterInformation.PeakValues;
            return channelPeaks.Count > selectedChannelOffset ? channelPeaks[selectedChannelOffset] : InputPeak;
        }
        catch
        {
            return InputPeak;
        }
    }

    /// <summary>
    /// Gets peak value for the right stereo channel.
    /// </summary>
    public float GetRightPeak()
    {
        if (inputDevice == null) return 0f;
        try
        {
            var channelPeaks = inputDevice.AudioMeterInformation.PeakValues;
            int rightChannel = SourceFormat?.Channels == 1 ? 0 : selectedChannelOffset + 1;
            return channelPeaks.Count > rightChannel ? channelPeaks[rightChannel] : GetLeftPeak();
        }
        catch
        {
            return InputPeak;
        }
    }

    /// <summary>
    /// Starts the capture and playback relay pipeline.
    /// </summary>
    public void Start(Endpoint selectedInput, Endpoint selectedOutput, int channelPairOffset, int targetLatencyMs, float initialGain)
    {
        if (loopbackCapture != null) throw new InvalidOperationException(UiText.T("监听已经启动。"));
        if (selectedInput.Id == selectedOutput.Id || selectedOutput.IsVirtual)
        {
            throw new InvalidOperationException(UiText.T("监听目标请选择真实耳机或扬声器，不能选回虚拟线路。"));
        }

        try
        {
            using var enumeratorInstance = new MMDeviceEnumerator();
            inputDevice = enumeratorInstance.GetDevice(selectedInput.Id);
            outputDevice = enumeratorInstance.GetDevice(selectedOutput.Id);

            loopbackCapture = new WasapiLoopbackCapture(inputDevice);
            sampleQueue = new BufferedWaveProvider(loopbackCapture.WaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(1),
                DiscardOnBufferOverflow = true,
                ReadFully = true
            };

            var activeQueue = sampleQueue;
            loopbackCapture.DataAvailable += (_, eventArgs) =>
            {
                if (activeQueue.BufferedDuration.TotalMilliseconds > Math.Max(250, targetLatencyMs * 2))
                {
                    activeQueue.ClearBuffer();
                    Interlocked.Increment(ref bufferUnderrunEvents);
                }
                activeQueue.AddSamples(eventArgs.Buffer, 0, eventArgs.BytesRecorded);
                Interlocked.Add(ref totalBytesRecorded, eventArgs.BytesRecorded);
            };

            loopbackCapture.RecordingStopped += HandleCaptureStopped;

            var stereoExtractor = new StereoPairProvider(sampleQueue.ToSampleProvider(), channelPairOffset);
            selectedChannelOffset = channelPairOffset;
            volumeProvider = new SmoothGainProvider(stereoExtractor, initialGain);

            playbackOut = new WasapiOut(outputDevice, AudioClientShareMode.Shared, true, targetLatencyMs);
            playbackOut.PlaybackStopped += HandlePlaybackStopped;
            playbackOut.Init(volumeProvider.ToWaveProvider());

            loopbackCapture.StartRecording();
            playbackOut.Play();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Adjusts the output playback volume.
    /// </summary>
    public void SetVolume(float newGain)
    {
        if (volumeProvider != null)
        {
            volumeProvider.SetVolume(newGain);
        }
    }

    private void HandleCaptureStopped(object? sender, StoppedEventArgs stopArgs)
    {
        if (stopArgs.Exception != null)
        {
            Failed?.Invoke(stopArgs.Exception);
        }
    }

    private void HandlePlaybackStopped(object? sender, StoppedEventArgs stopArgs)
    {
        if (stopArgs.Exception != null)
        {
            Failed?.Invoke(stopArgs.Exception);
        }
    }

    public void Dispose()
    {
        try
        {
            if (loopbackCapture != null)
            {
                loopbackCapture.RecordingStopped -= HandleCaptureStopped;
                loopbackCapture.Dispose();
                loopbackCapture = null;
            }
        }
        finally
        {
            try
            {
                if (playbackOut != null)
                {
                    playbackOut.PlaybackStopped -= HandlePlaybackStopped;
                    playbackOut.Dispose();
                    playbackOut = null;
                }
            }
            finally
            {
                inputDevice?.Dispose();
                outputDevice?.Dispose();
                inputDevice = null;
                outputDevice = null;
                volumeProvider = null;
                sampleQueue = null;
            }
        }
    }
}
