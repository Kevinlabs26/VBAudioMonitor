using NAudio.Wave;

namespace VBAudioMonitor;

/// <summary>
/// Extracts a stereo channel pair from a multi-channel audio stream.
/// </summary>
internal sealed class StereoPairProvider : ISampleProvider
{
    private readonly ISampleProvider upstreamProvider;
    private readonly int primaryChannelIndex;
    private float[] intermediateBuffer = [];

    public WaveFormat WaveFormat { get; }

    public StereoPairProvider(ISampleProvider sourceProvider, int requestedChannelIndex)
    {
        if (requestedChannelIndex < 0 || requestedChannelIndex >= sourceProvider.WaveFormat.Channels
            || (sourceProvider.WaveFormat.Channels > 1 && requestedChannelIndex + 1 >= sourceProvider.WaveFormat.Channels))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedChannelIndex));
        }

        upstreamProvider = sourceProvider;
        primaryChannelIndex = requestedChannelIndex;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sourceProvider.WaveFormat.SampleRate, 2);
    }

    /// <summary>
    /// Reads requested stereo samples by demuxing from the upstream multi-channel source.
    /// </summary>
    public int Read(float[] targetBuffer, int bufferOffset, int sampleCount)
    {
        int upstreamChannelCount = upstreamProvider.WaveFormat.Channels;
        int requiredSourceSamples = sampleCount / 2 * upstreamChannelCount;

        if (intermediateBuffer.Length < requiredSourceSamples)
        {
            intermediateBuffer = new float[requiredSourceSamples];
        }

        int samplesRead = upstreamProvider.Read(intermediateBuffer, 0, requiredSourceSamples);
        int completedFrames = samplesRead / upstreamChannelCount;

        for (int frameIndex = 0; frameIndex < completedFrames; frameIndex++)
        {
            int baseSourceIndex = frameIndex * upstreamChannelCount;
            int baseTargetIndex = bufferOffset + frameIndex * 2;

            targetBuffer[baseTargetIndex] = intermediateBuffer[baseSourceIndex + primaryChannelIndex];
            targetBuffer[baseTargetIndex + 1] = intermediateBuffer[baseSourceIndex + (upstreamChannelCount == 1 ? 0 : primaryChannelIndex + 1)];
        }

        return completedFrames * 2;
    }
}
