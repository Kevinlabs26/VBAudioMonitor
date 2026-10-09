using NAudio.Wave;

namespace VBAudioMonitor;

internal sealed class SmoothGainProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly int channels;
    private readonly int changeRampFrames;
    private float targetGain;
    private float rampTarget;
    private double currentGain;
    private double gainStep;
    private int framesRemaining;
    private int channelPosition;

    public WaveFormat WaveFormat => source.WaveFormat;

    public SmoothGainProvider(ISampleProvider source, float initialGain)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.WaveFormat.Channels <= 0 || source.WaveFormat.SampleRate <= 0)
            throw new ArgumentException(UiText.T("音频格式必须包含有效的声道和采样率。"), nameof(source));

        this.source = source;
        channels = source.WaveFormat.Channels;
        changeRampFrames = Math.Max(1, (int)Math.Round(source.WaveFormat.SampleRate * 0.03));
        framesRemaining = Math.Max(1, (int)Math.Round(source.WaveFormat.SampleRate * 0.2));
        targetGain = rampTarget = ClampGain(initialGain);
        gainStep = rampTarget / (double)framesRemaining;
    }

    public void SetVolume(float gain) => Volatile.Write(ref targetGain, ClampGain(gain));

    private static float ClampGain(float gain) => float.IsFinite(gain) ? Math.Clamp(gain, 0f, 1f) : 0f;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);
        for (int sample = offset; sample < offset + samplesRead; sample++)
        {
            if (channelPosition == 0)
            {
                float nextTarget = Volatile.Read(ref targetGain);
                if (nextTarget != rampTarget)
                {
                    rampTarget = nextTarget;
                    framesRemaining = changeRampFrames;
                    gainStep = (rampTarget - currentGain) / framesRemaining;
                }
                if (nextTarget == 0f)
                {
                    currentGain = 0;
                    framesRemaining = 0;
                }
            }

            buffer[sample] *= (float)currentGain;
            if (++channelPosition == channels)
            {
                channelPosition = 0;
                if (framesRemaining > 0)
                {
                    currentGain = --framesRemaining == 0 ? rampTarget : Math.Clamp(currentGain + gainStep, 0d, 1d);
                }
            }
        }
        return samplesRead;
    }
}
