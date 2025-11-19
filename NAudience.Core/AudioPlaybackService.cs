using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAudience.Core
{
    internal sealed class SwitchingSampleProvider : ISampleProvider
    {
        private ISampleProvider? current;
        private readonly WaveFormat outputFormat;
        private readonly object gate = new();

        public SwitchingSampleProvider(WaveFormat outputFormat)
        {
            this.outputFormat = outputFormat ?? throw new ArgumentNullException(nameof(outputFormat));
        }

        public WaveFormat WaveFormat => this.outputFormat;

        public void SetCurrent(ISampleProvider? provider)
        {
            lock (this.gate)
            {
                this.current = provider;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            ISampleProvider? p;
            lock (this.gate) { p = this.current; }
            if (p == null) { Array.Clear(buffer, offset, count); return count; }
            return p.Read(buffer, offset, count);
        }
    }

    // Einfacher Provider, der aus einem Float-Array (interleaved) liest
    internal sealed class ArraySampleProvider : ISampleProvider
    {
        private readonly float[] data;
        private long position; // in Samples (floats)
        public WaveFormat WaveFormat { get; }

        public ArraySampleProvider(float[] data, int sampleRate, int channels, long startSampleIndex = 0)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            if (channels <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(channels));
            }

            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            this.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            this.position = Math.Clamp(startSampleIndex, 0, data.LongLength);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesAvailable = (int) Math.Min(count, this.data.LongLength - this.position);
            if (samplesAvailable <= 0)
            {
                Array.Clear(buffer, offset, count);
                return 0;
            }
            Array.Copy(this.data, (int) this.position, buffer, offset, samplesAvailable);
            this.position += samplesAvailable;
            return samplesAvailable;
        }
    }

    public sealed class AudioPlaybackService : IDisposable
    {
        private readonly WaveOutEvent player; // von außen injiziert oder intern erzeugt
        private readonly bool ownsPlayer;
        private AudioFileReader? reader; // float32 Quelle (Datei) optional
        private SwitchingSampleProvider? switching; // konstanter Output (Geräteformat)
        private SampleToWaveProvider? waveProvider; // für WaveOutEvent.Init
        private ISampleProvider? pipeline; // aktuelle (resampled) Pipeline
        private readonly object graphGate = new();
        private float[]? rawData; // store original data for seeking while paused
        private int rawSampleRate;
        private int rawChannels;

        public float PlaybackRate { get; private set; } = 1.0f;
        public int DeviceSampleRate { get; private set; } = 44100;
        public int Channels { get; private set; } = 2;

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public AudioPlaybackService()
        {
            this.player = new WaveOutEvent();
            this.ownsPlayer = true;
            this.player.PlaybackStopped += (s, e) => this.PlaybackStopped?.Invoke(this, e);
        }

        public AudioPlaybackService(WaveOutEvent player)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.ownsPlayer = false;
            this.player.PlaybackStopped += (s, e) => this.PlaybackStopped?.Invoke(this, e);
        }

        public async Task InitializePlayback(string filePath, int? deviceSampleRate = null, int desiredLatency = 50, float initialVolume = 1.0f)
        {
            this.ResetGraph();

            // Quelle laden (AudioFileReader liefert Float32, normalisiert)
            this.reader = new AudioFileReader(filePath);
            this.Channels = this.reader.WaveFormat.Channels;
            this.DeviceSampleRate = deviceSampleRate ?? this.reader.WaveFormat.SampleRate;

            // Konstantes Geräteformat
            var deviceFormat = WaveFormat.CreateIeeeFloatWaveFormat(this.DeviceSampleRate, this.Channels);

            // Umschaltbarer Provider mit konstantem Format
            this.switching = new SwitchingSampleProvider(deviceFormat);

            // Erste Pipeline (PlaybackRate =1.0)
            this.pipeline = BuildPipeline(this.reader, this.PlaybackRate, deviceFormat);
            this.switching.SetCurrent(this.pipeline);

            // WaveOut initialisieren (falls noch nicht)
            this.player.DesiredLatency = desiredLatency;
            this.player.Volume = Math.Clamp(initialVolume, 0f, 1f);
            this.waveProvider = new SampleToWaveProvider(this.switching);
            this.player.Init(this.waveProvider);

            // Start (nicht blocking)
            await Task.Run(() => this.player.Play());
        }

        public async Task InitializePlayback(float[] data, int sampleRate, int channels, long startSampleIndex = 0, int? deviceSampleRate = null, int desiredLatency = 50, float initialVolume = 1.0f)
        {
            this.ResetGraph();

            this.rawData = data; // keep reference
            this.rawSampleRate = sampleRate;
            this.rawChannels = channels;

            this.Channels = channels;
            this.DeviceSampleRate = deviceSampleRate ?? sampleRate;

            var source = new ArraySampleProvider(data, sampleRate, channels, startSampleIndex);
            var deviceFormat = WaveFormat.CreateIeeeFloatWaveFormat(this.DeviceSampleRate, this.Channels);

            this.switching = new SwitchingSampleProvider(deviceFormat);
            this.pipeline = BuildPipeline(source, this.PlaybackRate, deviceFormat);
            this.switching.SetCurrent(this.pipeline);

            this.player.DesiredLatency = desiredLatency;
            this.player.Volume = Math.Clamp(initialVolume, 0f, 1f);
            this.waveProvider = new SampleToWaveProvider(this.switching);
            this.player.Init(this.waveProvider);

            await Task.Run(() => this.player.Play());
        }

        // Nahtlose Anpassung der Geschwindigkeit (Pitch & Tempo ändern sich gemeinsam, "Varispeed")
        public async Task AdjustSampleRate(float factor)
        {
            if (this.switching == null)
            {
                return;
            }

            this.PlaybackRate = factor;

			// Neue Pipeline auf Basis derselben Quelle aufbauen und atomar umschalten
			ISampleProvider? currentSource;
            lock (this.graphGate)
            {
                currentSource = this.pipeline; // aktuelle Pipeline-Quelle ist die erste Stufe der Kette
            }
            if (currentSource == null)
            {
                return;
            }

            // currentSource ist bereits das Ergebnis vorheriger Resampler.
            // Baue die Pipeline neu basierend auf der ursprünglichen Quelle, wenn möglich.
            // Sicherer: aus reader oder aus switching.WaveFormat nicht möglich, daher
            // wir nutzen die gleiche Logik wie Initialize, aber mit dem Eingang der letzten Quelle,
            // falls das die ursprüngliche Quelle ist. Besser: reader ?? pipeline als Quelle verwenden.
            ISampleProvider baseSource = this.reader ?? currentSource;
            var newPipeline = BuildPipeline(baseSource, this.PlaybackRate, this.switching.WaveFormat);
            lock (this.graphGate)
            {
                this.pipeline = newPipeline;
                this.switching.SetCurrent(this.pipeline);
            }

            await Task.CompletedTask; // API bleibt async
        }

        // Seek in paused state without reinitializing WaveOut
        public void SeekSamples(long startSampleIndex)
        {
            if (this.switching == null || this.rawData == null || this.rawSampleRate <= 0 || this.rawChannels <= 0)
            {
                return;
            }
            // Clamp startSampleIndex
            startSampleIndex = Math.Clamp(startSampleIndex, 0, this.rawData.LongLength);
            var source = new ArraySampleProvider(this.rawData, this.rawSampleRate, this.rawChannels, startSampleIndex);
            var newPipeline = BuildPipeline(source, this.PlaybackRate, this.switching.WaveFormat);
            lock (this.graphGate)
            {
                this.pipeline = newPipeline;
                this.switching.SetCurrent(this.pipeline);
            }
        }

        // Graph aufbauen: Quelle -> (Resample auf R*f) -> (Resample auf DeviceRate) -> konstant D
        private static ISampleProvider BuildPipeline(ISampleProvider source, double rate, WaveFormat deviceFormat)
        {
            //1) Quelle ggf. auf "virtuelle" Abtastrate R * rate bringen (erzeugt Varispeed-Effekt)
            int sourceRate = source.WaveFormat.SampleRate;
            int channels = source.WaveFormat.Channels;

            // WdlResamplingSampleProvider erzeugt einen Provider mit neuem WaveFormat (SampleRate)
            var spedUp = new WdlResamplingSampleProvider(source, Math.Max(8000, (int) Math.Round(sourceRate * rate)));

            //2) Auf die konstante Device-Rate zurück resamplen
            var toDevice = new WdlResamplingSampleProvider(spedUp, deviceFormat.SampleRate);

            // Sicherheitscheck: Kanäle konsistent halten
            if (toDevice.WaveFormat.Channels != deviceFormat.Channels)
            {
                if (deviceFormat.Channels == 1 && channels > 1)
                {
                    toDevice = new WdlResamplingSampleProvider(
                        new StereoToMonoSampleProvider(spedUp) { LeftVolume = 0.5f, RightVolume = 0.5f },
                        deviceFormat.SampleRate);
                }
                // sonst: beibehalten, typ. wandelt das Ausgabegerät im Shared-Mode
            }

            return toDevice;
        }

        public void Stop()
        {
            this.player?.Stop();
        }

        public void Pause()
        {
            if (this.player.PlaybackState == PlaybackState.Playing)
            {
                this.player.Pause();
            }
        }

        public void Resume()
        {
            if (this.player.PlaybackState == PlaybackState.Paused)
            {
                this.player.Play();
            }
        }

        public long GetPositionBytes()
        {
            try { return this.player.GetPosition(); } catch { return 0; }
        }

        public void SetVolume(float volume)
        {
            this.player.Volume = Math.Clamp(volume, 0f, 1f);
        }

        private void ResetGraph()
        {
            this.player.Stop();
            this.reader?.Dispose();
            this.reader = null;
            this.switching = null;
            this.waveProvider = null;
            this.pipeline = null;
            this.rawData = null;
            this.rawSampleRate = 0;
            this.rawChannels = 0;
        }

        public void Dispose()
        {
            try { this.player.Stop(); } catch { }
            this.reader?.Dispose();
            this.reader = null;
            this.switching = null;
            this.waveProvider = null;
            this.pipeline = null;
            if (this.ownsPlayer)
            {
                this.player.Dispose();
            }
        }
    }
}
