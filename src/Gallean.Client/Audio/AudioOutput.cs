using System.Runtime.ExceptionServices;
using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.Alsa;
using NAudio.Wave.SampleProviders;

namespace Gallean.Client;

internal sealed class AudioOutput : IDisposable
{
    private readonly ILog log;
    private readonly MixingSampleProvider mixer = CreateMixer();
    private IWavePlayer? device;
    private ReadAheadWaveProvider? readAhead;

    public AudioOutput(ILog log)
    {
        this.log = log;

        Start();
    }

    public bool IsOpen => device is not null;

    public void AddInput(ISampleProvider input)
    {
        mixer.AddMixerInput(input);
    }

    public void Dispose()
    {
        try
        {
            device?.Dispose();
        }
        finally
        {
            device = null;
            readAhead?.Dispose();
            readAhead = null;
        }
    }

    private static MixingSampleProvider CreateMixer()
    {
        WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SoundDecoder.MixerSampleRate, SoundDecoder.MixerChannels);

        return new MixingSampleProvider(waveFormat)
        {
            ReadFully = true
        };
    }

    private static IWavePlayer CreateDevice()
    {
        if (OperatingSystem.IsWindows())
        {
            WasapiPlayerBuilder builder = new();

            return builder.Build();
        }
        else if (OperatingSystem.IsLinux())
        {
            return new AlsaOut();
        }
        else if (OperatingSystem.IsMacOSVersionAtLeast(10, 5))
        {
            return new CoreAudioPlayer();
        }
        else
        {
            throw new PlatformNotSupportedException("No sound output is available on this platform.");
        }
    }

    private void Start()
    {
        try
        {
            device = CreateDevice();
            device.PlaybackStopped += OnPlaybackStopped;
            if (OperatingSystem.IsMacOS())
            {
                // Keep decoding off Core Audio's deadline-sensitive callback.
                readAhead = new ReadAheadWaveProvider(new SampleToWaveProvider(mixer));
                device.Init(readAhead);
            }
            else
            {
                device.Init(mixer);
            }
            device.Play();
        }
        catch (Exception ex)
        {
            Dispose();

            log.Error("Failed to initialize sound output.", ex);
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            Exception ex = e.Exception;

            Dispatcher.UIThread.Post(() =>
            {
                if (ex is SoundDecodeException decodeException)
                {
                    ExceptionDispatchInfo.Capture(decodeException).Throw();
                }
                else
                {
                    Dispose();

                    log.Error("Sound output failed.", ex);
                }
            });
        }
    }
}
