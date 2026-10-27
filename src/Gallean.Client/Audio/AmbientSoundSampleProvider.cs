using NAudio.Wave;

namespace Gallean.Client;

internal sealed class AmbientSoundSampleProvider : ISampleProvider, IAmbientAudioPlayback
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(250);

    private readonly Sound sound;
    private readonly float gainPerFrame;
    private int position;
    private bool finished;
    private volatile float targetLeftGain;
    private volatile float targetRightGain;
    private float leftGain;
    private float rightGain;

    public AmbientSoundSampleProvider(Sound sound, float volume, float pan)
    {
        this.sound = sound;

        gainPerFrame = 1.0f / (float)(SoundDecoder.MixerSampleRate * FadeDuration.TotalSeconds);

        SetLevel(volume, pan);
    }

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SoundDecoder.MixerSampleRate, SoundDecoder.MixerChannels);

    public void SetLevel(float volume, float pan)
    {
        targetLeftGain = volume * MathF.Sqrt(1 - pan);
        targetRightGain = volume * MathF.Sqrt(pan);
    }

    public void Stop()
    {
        targetLeftGain = 0;
        targetRightGain = 0;
    }

    public int Read(Span<float> buffer)
    {
        if (finished || sound.Samples.Length == 0)
        {
            return 0;
        }

        float targetLeft = targetLeftGain;
        float targetRight = targetRightGain;

        int count = 0;

        while (count < buffer.Length)
        {
            int available = Math.Min(buffer.Length - count, sound.Samples.Length - position);
            sound.Samples.AsSpan(position, available).CopyTo(buffer[count..]);
            count += available;
            position = (position + available) % sound.Samples.Length;
        }

        int frames = count / SoundDecoder.MixerChannels;

        for (int frame = 0; frame < frames; frame++)
        {
            leftGain = AdjustGain(leftGain, targetLeft);
            rightGain = AdjustGain(rightGain, targetRight);

            int index = frame * SoundDecoder.MixerChannels;

            buffer[index] *= leftGain;
            buffer[index + 1] *= rightGain;
        }

        if (targetLeft == 0 && targetRight == 0 && leftGain == 0 && rightGain == 0)
        {
            finished = true;
        }

        return count;
    }

    private float AdjustGain(float gain, float target)
    {
        if (gain < target)
        {
            return Math.Min(gain + gainPerFrame, target);
        }
        else
        {
            return Math.Max(gain - gainPerFrame, target);
        }
    }
}
