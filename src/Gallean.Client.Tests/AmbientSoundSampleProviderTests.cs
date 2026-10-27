namespace Gallean.Client.Tests;

public sealed class AmbientSoundSampleProviderTests
{
    [Test]
    public void LoopAcrossBufferBoundariesTest()
    {
        AmbientSoundSampleProvider provider = new(new Sound([1, -1, 0.5f, -0.5f]), 1, 0);
        float[] first = new float[6];
        float[] second = new float[6];

        Assert.That(provider.Read(first), Is.EqualTo(first.Length));
        Assert.That(provider.Read(second), Is.EqualTo(second.Length));

        float step = 1.0f / (SoundDecoder.MixerSampleRate * 0.25f);

        Assert.That(first, Is.EqualTo(new float[] { step, 0, step, 0, 3 * step, 0 }).Within(0.000001f));
        Assert.That(second, Is.EqualTo(new float[] { 2 * step, 0, 5 * step, 0, 3 * step, 0 }).Within(0.000001f));
    }

    [Test]
    public void StopFadesOutAndEndsTest()
    {
        AmbientSoundSampleProvider provider = new(new Sound([1, 1]), 1, 0);
        float[] buffer = new float[SoundDecoder.MixerSampleRate];
        provider.Read(buffer);

        provider.Stop();

        Assert.That(provider.Read(buffer), Is.EqualTo(buffer.Length));
        Assert.That(buffer[0], Is.GreaterThan(0).And.LessThan(1));
        Assert.That(buffer[^2], Is.Zero);
        Assert.That(provider.Read(buffer), Is.Zero);
    }

    [Test]
    public void EmptySoundEndsTest()
    {
        AmbientSoundSampleProvider provider = new(new Sound([]), 1, 0);

        Assert.That(provider.Read(new float[2]), Is.Zero);
    }
}
