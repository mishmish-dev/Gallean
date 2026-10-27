using System.Runtime.ExceptionServices;
using NAudio.Wave;

namespace Gallean.Client;

internal sealed class ReadAheadWaveProvider : IWaveProvider, IDisposable
{
    private readonly IWaveProvider source;
    private readonly BufferedWaveProvider buffer;
    private readonly CancellationTokenSource stop = new();
    private readonly Thread producer;
    private ExceptionDispatchInfo? failure;
    private int disposed;

    public ReadAheadWaveProvider(IWaveProvider source)
    {
        this.source = source;
        buffer = new BufferedWaveProvider(source.WaveFormat, TimeSpan.FromMilliseconds(100));
        producer = new Thread(FillBuffer) { IsBackground = true, Name = "Gallean audio decoder" };
        producer.Start();
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(Span<byte> output)
    {
        ExceptionDispatchInfo? error = Volatile.Read(ref failure);

        if (error is not null)
        {
            output.Clear();
            error.Throw();
        }

        return buffer.Read(output);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        stop.Cancel();
        producer.Join();
        stop.Dispose();
    }

    private void FillBuffer()
    {
        try
        {
            byte[] samples = new byte[WaveFormat.ConvertLatencyToByteSize(10)];

            while (!stop.IsCancellationRequested)
            {
                if (buffer.BufferLength - buffer.BufferedBytes < samples.Length)
                {
                    stop.Token.WaitHandle.WaitOne(5);
                    continue;
                }

                int read = source.Read(samples);

                if (read == 0)
                {
                    break;
                }

                buffer.AddSamples(samples.AsSpan(0, read));
            }
        }
        catch (Exception ex)
        {
            Volatile.Write(ref failure, ExceptionDispatchInfo.Capture(ex));
        }
    }
}
