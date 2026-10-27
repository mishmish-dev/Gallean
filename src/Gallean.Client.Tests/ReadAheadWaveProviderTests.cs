using NAudio.Wave;

namespace Gallean.Client.Tests;

public sealed class ReadAheadWaveProviderTests
{
    private sealed class TestSource : IWaveProvider, IDisposable
    {
        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim Continue { get; } = new();
        public Exception? Failure { get; init; }
        public int ReadThread { get; private set; }
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        public int Read(Span<byte> buffer)
        {
            ReadThread = Environment.CurrentManagedThreadId;
            Started.Set();
            Continue.Wait();

            if (Failure is not null)
            {
                throw Failure;
            }

            buffer.Fill(17);
            return buffer.Length;
        }

        public void Dispose()
        {
            Started.Dispose();
            Continue.Dispose();
        }
    }

    [Test]
    public void ReadDoesNotWaitForDecoderTest()
    {
        using TestSource source = new();
        using ReadAheadWaveProvider provider = new(source);

        try
        {
            Assert.That(source.Started.Wait(TimeSpan.FromSeconds(5)), Is.True);
            byte[] buffer = new byte[32];

            Assert.That(provider.Read(buffer), Is.EqualTo(buffer.Length));
            Assert.That(buffer, Is.All.Zero);
            Assert.That(source.ReadThread, Is.Not.EqualTo(Environment.CurrentManagedThreadId));

            source.Continue.Set();

            Assert.That(SpinWait.SpinUntil(() =>
            {
                provider.Read(buffer);
                return buffer[0] == 17;
            }, TimeSpan.FromSeconds(5)), Is.True);
        }
        finally
        {
            source.Continue.Set();
        }
    }

    [Test]
    public void DecoderFailureClearsOutputAndPreservesExceptionTest()
    {
        IOException failure = new("Decode failed.");
        using TestSource source = new() { Failure = failure };
        source.Continue.Set();
        using ReadAheadWaveProvider provider = new(source);
        byte[] buffer = new byte[32];
        Exception? actual = null;

        Assert.That(SpinWait.SpinUntil(() =>
        {
            buffer.AsSpan().Fill(255);

            try
            {
                provider.Read(buffer);
                return false;
            }
            catch (Exception ex)
            {
                actual = ex;
                return true;
            }
        }, TimeSpan.FromSeconds(5)), Is.True);

        Assert.That(actual, Is.SameAs(failure));
        Assert.That(buffer, Is.All.Zero);
    }
}
