using System.Runtime.Versioning;
using NAudio.Wave;

namespace Gallean.Client.Tests;

[Explicit("Uses the macOS audio output device.")]
[NonParallelizable]
[SupportedOSPlatform("macos")]
public sealed class MacAudioOutputTests
{
    private sealed class FailingSource(int failOnRead, Exception failure) : ISampleProvider
    {
        private int reads;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        public int Read(Span<float> buffer)
        {
            if (++reads == failOnRead)
            {
                throw failure;
            }

            buffer.Clear();
            return buffer.Length;
        }
    }

    [Test]
    public void StartupFailureTest()
    {
        InvalidDataException failure = new();
        FailingSource source = new(1, failure);

        Assert.That(Assert.Throws<InvalidDataException>(() => new MacAudioOutput(source, _ => { })), Is.SameAs(failure));
    }

    [Test]
    public async Task CallbackFailureTest()
    {
        SoundDecodeException failure = new(new AssetSoundKey.MenuMusic(), new InvalidDataException());
        FailingSource source = new(4, failure);
        TaskCompletionSource<Exception> stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using MacAudioOutput output = new(source, ex => stopped.SetResult(ex));

        Assert.That(await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.SameAs(failure));
        output.Dispose();
    }
}
