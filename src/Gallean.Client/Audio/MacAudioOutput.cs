using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NAudio.Wave;

namespace Gallean.Client;

[SupportedOSPlatform("macos")]
internal sealed unsafe partial class MacAudioOutput : IDisposable
{
    private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    private const uint LinearPcm = 0x6c70636d;
    private const uint FloatPacked = 1 | 8;

    private readonly ISampleProvider source;
    private readonly Action<Exception> onFailure;
    private GCHandle context;
    private IntPtr queue;
    private int stopped;

    public MacAudioOutput(ISampleProvider source, Action<Exception> onFailure)
    {
        this.source = source;
        this.onFailure = onFailure;

        WaveFormat format = source.WaveFormat;
        StreamDescription description = new()
        {
            SampleRate = format.SampleRate,
            FormatId = LinearPcm,
            FormatFlags = FloatPacked,
            BytesPerPacket = (uint)format.BlockAlign,
            FramesPerPacket = 1,
            BytesPerFrame = (uint)format.BlockAlign,
            ChannelsPerFrame = (uint)format.Channels,
            BitsPerChannel = 32
        };

        try
        {
            context = GCHandle.Alloc(this);
            Check(AudioQueueNewOutput(&description, &OnBufferAvailable, GCHandle.ToIntPtr(context), IntPtr.Zero, IntPtr.Zero, 0, out queue));

            uint bufferSize = (uint)(format.SampleRate / 20 * format.BlockAlign);

            for (int index = 0; index < 3; index++)
            {
                Check(AudioQueueAllocateBuffer(queue, bufferSize, out QueueBuffer* buffer));
                FillBuffer(buffer);
            }

            Check(AudioQueueStart(queue, IntPtr.Zero));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref stopped, 1);

        if (queue != IntPtr.Zero)
        {
            Check(AudioQueueDispose(queue, 1));
            queue = IntPtr.Zero;
        }

        if (context.IsAllocated)
        {
            context.Free();
        }
    }

    private void FillBuffer(QueueBuffer* buffer)
    {
        Span<float> samples = new(buffer->Data, (int)buffer->Capacity / sizeof(float));
        int read = source.Read(samples);
        samples[read..].Clear();
        buffer->Size = buffer->Capacity;

        Check(AudioQueueEnqueueBuffer(queue, buffer, 0, IntPtr.Zero));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnBufferAvailable(IntPtr userData, IntPtr audioQueue, QueueBuffer* buffer)
    {
        MacAudioOutput output = (MacAudioOutput)GCHandle.FromIntPtr(userData).Target!;

        if (Volatile.Read(ref output.stopped) != 0)
        {
            return;
        }

        try
        {
            output.FillBuffer(buffer);
        }
        catch (Exception ex)
        {
            if (Interlocked.Exchange(ref output.stopped, 1) == 0)
            {
                output.onFailure(ex);
            }
        }
    }

    private static void Check(int status)
    {
        if (status != 0)
        {
            throw new ExternalException($"Audio Queue failed ({status}).", status);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StreamDescription
    {
        public double SampleRate;
        public uint FormatId;
        public uint FormatFlags;
        public uint BytesPerPacket;
        public uint FramesPerPacket;
        public uint BytesPerFrame;
        public uint ChannelsPerFrame;
        public uint BitsPerChannel;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct QueueBuffer
    {
        public uint Capacity;
        public void* Data;
        public uint Size;
    }

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueNewOutput(StreamDescription* format, delegate* unmanaged[Cdecl]<IntPtr, IntPtr, QueueBuffer*, void> callback, IntPtr userData, IntPtr runLoop, IntPtr runLoopMode, uint flags, out IntPtr queue);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueAllocateBuffer(IntPtr queue, uint size, out QueueBuffer* buffer);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueEnqueueBuffer(IntPtr queue, QueueBuffer* buffer, uint packetCount, IntPtr packets);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueStart(IntPtr queue, IntPtr startTime);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueDispose(IntPtr queue, byte immediate);
}
