using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace NAudience.Core
{
	public class AudioObj : IDisposable
	{
		// Fields
		public Guid Id { get; set; } = Guid.NewGuid();
		public readonly DateTime CreatedAt = DateTime.UtcNow;
		public string Name { get; set; } = string.Empty;
		public string FilePath { get; set; } = string.Empty;

		public float[] Data { get; set; } = [];
		public int SampleRate { get; set; } = 0;
		public double SampleRateFactor { get; private set; } = 1.0;
		public int AdjustedSampleRate => (int) (this.SampleRate * this.SampleRateFactor);
		public int Channels { get; set; } = 0;
		public int BitDepth { get; set; } = 0;
		public long Length { get; set; } = 0;
		public TimeSpan Duration { get; set; } = TimeSpan.Zero;

		public float Bpm { get; set; } = 0.0f;
		public float ScannedBpm { get; set; } = 0.0f;
		public float Timing { get; set; } = 1.0f;
		public float ScannedTiming { get; set; } = 1.0f;
		public float Volume { get; set; } = 1.0f;

		public bool Playing { get; private set; } = false;
		public bool Paused { get; private set; } = false;

		public int ChunkSize { get; set; } = 0;
		public int OverlapSize { get; set; } = 0;
		public double StretchFactor { get; set; } = 1.0;

		public long SkippedPositionBytes { get; private set; } = 0;

		public long ScrollOffset { get; set; } = 0;
		public long StartingOffset { get; set; } = 0;
		public long SelectionStart { get; set; } = -1;
		public long SelectionEnd { get; set; } = -1;
		public int LastSamplesPerPixel { get; set; } = 0;
		public string SampleTag { get; set; } = string.Empty;
		public BindingList<AudioObj> PreviousSteps = [];
		public BindingList<AudioObj> NextSteps = [];

		// Playback service (neu)
		private readonly AudioPlaybackService playback = new();

		// Properties
		private long positionOriginBytes = 0;
		private bool resumeFromSetPosition = false; // minimal: reinit on resume if position changed while paused
		private long pausedBaselineBytes = 0; // store baseline at pause for accurate delta after seek while paused

		// Enums
		public Dictionary<string, double> Metrics { get; private set; } = [];
		public double this[string metric]
		{
			get
			{
				// Find by tolower case
				if (this.Metrics.TryGetValue(metric, out double value))
				{
					return value;
				}
				else
				{
					var key = this.Metrics.Keys.FirstOrDefault(k => k.Equals(metric, StringComparison.OrdinalIgnoreCase));
					if (key != null)
					{
						return this.Metrics[key];
					}
					else
					{
						// If not found, return 0.0
						return 0.0;
					}
				}
			}
			set
			{
				// Find by tolower case
				if (this.Metrics.ContainsKey(metric))
				{
					this.Metrics[metric] = value;
				}
				else
				{
					var key = this.Metrics.Keys.FirstOrDefault(k => k.Equals(metric, StringComparison.OrdinalIgnoreCase));
					if (key != null)
					{
						this.Metrics[key] = value;
					}
					else
					{
						// Capitalize first letter and add to dictionary
						string capitalizedMetric = char.ToUpper(metric[0]) + metric.Substring(1).ToLowerInvariant();
						this.Metrics.Add(capitalizedMetric, value);
					}
				}
			}
		}

		// Lambda
		public bool PlayerPlaying => this.Playing && !this.Paused;

		// Konsistente aktuelle Byte-Position (absolut)
		public long CurrentPlaybackPositionBytes
		{
			get
			{
				if (this.PlayerPlaying)
				{
					long gp = 0;
					try { gp = this.playback.GetPositionBytes(); } catch { gp = 0; }
					long delta = gp - this.positionOriginBytes;
					if (delta < 0) { delta = 0; }
					return this.SkippedPositionBytes + delta;
				}
				return this.SkippedPositionBytes;
			}
			private set
			{
				// Clamp auf Datenlänge, ohne auf einen WaveStream angewiesen zu sein
				int ch = Math.Max(1, this.Channels);
				int bytesPerFrame = ch * sizeof(float);
				long totalFrames = (this.Data?.LongLength ?? 0L) / ch;
				long totalBytes = totalFrames * bytesPerFrame;

				long target = Math.Clamp(value, 0, totalBytes);
				this.SkippedPositionBytes = target;
			}
		}

		// Position in Frames (Samples pro Kanal)
		public long Position
		{
			get
			{
				long positionBytes = this.CurrentPlaybackPositionBytes;
				int bytesPerFrame = Math.Max(1, this.Channels) * sizeof(float);
				return bytesPerFrame > 0 ? positionBytes / bytesPerFrame : 0;
			}
		}
		public TimeSpan CurrentTime => TimeSpan.FromSeconds((double) this.Position / Math.Max(1, this.SampleRate));
		public double SizeInKb => this.Data.Length * sizeof(float) / 1024.0;

		// Ctor
		public AudioObj()
		{
			// Empty
		}

		public AudioObj(string filePath, bool load = false)
		{
			this.FilePath = filePath;

			if (load)
			{
				if (this.LoadAudioFile())
				{
					return;

				}
				else
				{
					this.Dispose();
				}
			}
		}

		// Static factory
		public static AudioObj? FromFile(string filePath)
		{
			var obj = new AudioObj(filePath);
			if (obj.LoadAudioFile())
			{
				return obj;
			}
			else
			{
				return null;
			}
		}

		public static async Task<AudioObj?> FromFileAsync(string filePath)
		{
			var obj = await Task.Run(() => new AudioObj(filePath));
			if (obj.LoadAudioFile())
			{
				return obj;
			}
			else
			{
				obj?.Dispose();
				return null;
			}
		}

		// Clone
		public AudioObj Clone()
		{
			AudioObj clone = new()
			{
				Id = Guid.NewGuid(),
				Name = this.Name,
				FilePath = this.FilePath,
				Data = (float[]) this.Data.Clone(),
				SampleRate = this.SampleRate,
				Channels = this.Channels,
				BitDepth = this.BitDepth,
				Length = this.Length,
				Duration = this.Duration,
				Bpm = this.Bpm,
				Timing = this.Timing,
				Volume = this.Volume
			};

			return clone;
		}

		public async Task<AudioObj> CloneAsync()
		{
			return await Task.Run(() => this.Clone());
		}

		public async Task<AudioObj?> CloneFromSelectionAsync()
		{
			if (this.Data == null || this.Data.LongLength <= 0 || this.SelectionEnd < 0 || this.SelectionStart < 0 || this.SelectionStart == this.SelectionEnd)
			{
				return null;
			}

			if (this.SelectionEnd < this.SelectionStart)
			{
				long swap = this.SelectionEnd;
				this.SelectionEnd = this.SelectionStart;
				this.SelectionStart = swap;
			}

			int channels = Math.Max(1, this.Channels);
			long totalSamples = this.Data.LongLength; // interleaved samples
			long selStartSample = Math.Clamp(this.SelectionStart, 0, totalSamples);
			long selEndSample = Math.Clamp(this.SelectionEnd, 0, totalSamples);
			long selSampleCount = selEndSample - selStartSample; // interleaved sample count

			if (selSampleCount <= 0)
			{
				return null;
			}

			AudioObj clone = new()
			{
				Name = this.Name + "_selection",
				Data = new float[selSampleCount],
				SampleRate = this.SampleRate,
				Channels = this.Channels,
				BitDepth = this.BitDepth,
				Bpm = this.Bpm,
				Timing = this.Timing,
				Volume = this.Volume,
				Length = selSampleCount,
				Duration = TimeSpan.FromSeconds((double) selSampleCount / (this.SampleRate * channels))
			};

			Buffer.BlockCopy(
				src: this.Data,
				srcOffset: (int) (selStartSample * sizeof(float)),
				dst: clone.Data,
				dstOffset: 0,
				count: (int) (selSampleCount * sizeof(float))
			);

			await Task.CompletedTask;

			return clone;
		}

		// IO Methods
		public void Dispose()
		{
			this.Playing = false;
			this.Paused = false;

			this.Data = [];

			// Player stoppen/aufräumen
			try { this.playback.Stop(); } catch { }

			GC.SuppressFinalize(this);
		}

		public bool LoadAudioFile()
		{
			if (string.IsNullOrEmpty(this.FilePath))
			{
				return false;
			}

			this.Name = Path.GetFileNameWithoutExtension(this.FilePath);

			Stopwatch sw = Stopwatch.StartNew();

			try
			{
				using var reader = new AudioFileReader(this.FilePath);
				this.SampleRate = reader.WaveFormat.SampleRate;
				this.Channels = reader.WaveFormat.Channels;
				this.BitDepth = reader.WaveFormat.BitsPerSample;

				long numSamples = 0;
				if (reader.Length > 0 && reader.WaveFormat.BitsPerSample > 0)
				{
					numSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
				}
				if (numSamples > 0)
				{
					try
					{
						float[] tmp = new float[numSamples];
						int read = reader.Read(tmp, 0, (int) numSamples);
						if (read != numSamples)
						{
							float[] resized = new float[read];
							Array.Copy(tmp, resized, read);
							this.Data = resized;
						}
						else
						{
							this.Data = tmp;
						}
					}
					catch
					{
						this.Data = ReadAllSamplesStreaming(reader).ToArray();
					}
				}
				else
				{
					this.Data = ReadAllSamplesStreaming(reader).ToArray();
				}

				this.Length = this.Data.Length;
				this.Duration = reader.TotalTime;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error loading audio file: {ex.Message}");
				this.Dispose();
				return false;
			}

			this["Import"] = sw.Elapsed.TotalMilliseconds;
			sw.Restart();

			this.ReadBpmTag();

			this["ReadBpmTag"] = sw.Elapsed.TotalMilliseconds;
			sw.Stop();

			return true;
		}

		public float ReadBpmTag(string tag = "TBPM", bool set = true)
		{
			// Read bpm metadata if available
			float bpm = 0.0f;
			float roughBpm = 0.0f;

			try
			{
				if (!string.IsNullOrEmpty(this.FilePath) && File.Exists(this.FilePath))
				{
					using var file = TagLib.File.Create(this.FilePath);
					if (file.Tag.BeatsPerMinute > 0)
					{
						roughBpm = (float) file.Tag.BeatsPerMinute;
					}
					if (file.TagTypes.HasFlag(TagLib.TagTypes.Id3v2))
					{
						var id3v2Tag = (TagLib.Id3v2.Tag) file.GetTag(TagLib.TagTypes.Id3v2);

						var tagTextFrame = TagLib.Id3v2.TextInformationFrame.Get(id3v2Tag, tag, false);

						if (tagTextFrame != null && tagTextFrame.Text.Any())
						{
							string bpmString = tagTextFrame.Text.FirstOrDefault() ?? "0,0";
							if (!string.IsNullOrEmpty(bpmString))
							{
								bpmString = bpmString.Replace(',', '.');

								if (float.TryParse(bpmString, NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedBpm))
								{
									bpm = parsedBpm;
								}
							}
						}
						else
						{
							bpm = 0.0f;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Fehler beim Lesen des Tags {tag.ToUpper()}: {ex.Message} ({ex.InnerException?.Message ?? " - "})");
			}

			// Take rough bpm if <= 0.0f
			if (bpm <= 0.0f && roughBpm > 0.0f)
			{
				Console.WriteLine($"No value found for '{tag.ToUpper()}', taking rough BPM value from legacy tag.");
				bpm = roughBpm;
			}

			if (set)
			{
				this.Bpm = bpm;
				if (this.Bpm <= 10)
				{
					this.ReadBpmTagLegacy();
				}
			}

			return bpm;
		}

		public float ReadBpmTagLegacy()
		{
			// Read bpm metadata if available
			float bpm = 0.0f;

			try
			{
				if (!string.IsNullOrEmpty(this.FilePath) && File.Exists(this.FilePath))
				{
					using var file = TagLib.File.Create(this.FilePath);
					// Check for BPM in standard ID3v2 tag
					if (file.Tag.BeatsPerMinute > 0)
					{
						bpm = (float) file.Tag.BeatsPerMinute;
					}
					// Alternative für spezielle Tags (z.B. TBPM Frame)
					else if (file.TagTypes.HasFlag(TagLib.TagTypes.Id3v2))
					{
						var id3v2Tag = (TagLib.Id3v2.Tag) file.GetTag(TagLib.TagTypes.Id3v2);
						var bpmFrame = TagLib.Id3v2.UserTextInformationFrame.Get(id3v2Tag, "BPM", false);

						if (bpmFrame != null && float.TryParse(bpmFrame.Text.FirstOrDefault(), out float parsedBpm))
						{
							bpm = parsedBpm;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Fehler beim Lesen der BPM: {ex.Message}");
			}
			this.Bpm = bpm > 0 ? bpm / 100.0f : 0.0f;
			return this.Bpm;
		}

		public async Task CreateSnapshotAsync()
		{
			this.PreviousSteps.Add(await this.CloneAsync());
		}



		// Data Methods
		public async Task<byte[]> GetBytesAsync(int maxWorkers = 4)
		{
			maxWorkers = Math.Clamp(maxWorkers, 1, Environment.ProcessorCount);

			if (this.Data == null || this.Data.Length == 0)
			{
				return [];
			}

			int bytesPerSample = this.BitDepth / 8;
			byte[] result = new byte[this.Data.Length * bytesPerSample];

			await Task.Run(() =>
			{
				var options = new ParallelOptions
				{
					MaxDegreeOfParallelism = maxWorkers
				};

				Parallel.For(0, this.Data.Length, options, i =>
				{
					float sample = this.Data[i];

					switch (this.BitDepth)
					{
						case 8:
							result[i] = (byte) (sample * 127f);
							break;

						case 16:
							short sample16 = (short) (sample * short.MaxValue);
							Span<byte> target16 = result.AsSpan(i * 2, 2);
							BitConverter.TryWriteBytes(target16, sample16);
							break;

						case 24:
							int sample24 = (int) (sample * 8_388_607f); // 2^23 - 1
							Span<byte> target24 = result.AsSpan(i * 3, 3);
							target24[0] = (byte) sample24;
							target24[1] = (byte) (sample24 >> 8);
							target24[2] = (byte) (sample24 >> 16);
							break;

						case 32:
							Span<byte> target32 = result.AsSpan(i * 4, 4);
							BitConverter.TryWriteBytes(target32, sample);
							break;
					}
				});
			});

			return result;
		}

		public async Task<IEnumerable<float[]>> GetChunksAsync(int size = 2048, float overlap = 0.5f, bool keepData = false, int maxWorkers = 4)
		{
			maxWorkers = Math.Clamp(maxWorkers, 1, Environment.ProcessorCount);

			// Input Validation (sync part for fast fail)
			if (this.Data == null || this.Data.Length == 0)
			{
				return [];
			}

			if (size <= 0 || overlap < 0 || overlap >= 1)
			{
				return [];
			}

			// Calculate chunk metrics (sync)
			this.ChunkSize = size;
			this.OverlapSize = (int) (size * overlap);
			int step = size - this.OverlapSize;
			int numChunks = (this.Data.Length - size) / step + 1;

			// Prepare result array
			float[][] chunks = new float[numChunks][];

			await Task.Run(() =>
			{
				// Parallel processing with optimal worker count
				Parallel.For(0, numChunks, new ParallelOptions
				{
					MaxDegreeOfParallelism = maxWorkers
				}, i =>
				{
					int sourceOffset = i * step;
					float[] chunk = new float[size];
					Buffer.BlockCopy(
						src: this.Data,
						srcOffset: sourceOffset * sizeof(float),
						dst: chunk,
						dstOffset: 0,
						count: size * sizeof(float));
					chunks[i] = chunk;
				});
			});

			// Cleanup if requested
			if (!keepData)
			{
				this.Data = [];
			}

			return chunks;
		}

		public async Task AggregateStretchedChunksAsync(IEnumerable<float[]> chunks, double stretchFactor = 1.0, int maxWorkers = 4)
		{
			maxWorkers = Math.Clamp(maxWorkers, 1, Environment.ProcessorCount);

			if (chunks == null || !chunks.Any())
			{
				return;
			}

			this.StretchFactor = stretchFactor;

			// Pre-calculate all values that don't change
			int chunkSize = this.ChunkSize;
			int overlapSize = this.OverlapSize;
			int originalHopSize = chunkSize - overlapSize;
			int stretchedHopSize = (int) Math.Round(originalHopSize * stretchFactor);
			int outputLength = (chunks.Count() - 1) * stretchedHopSize + chunkSize;

			// Create window function (cosine window)
			double[] window = await Task.Run(() =>
				Enumerable.Range(0, chunkSize)
						  .Select(i => 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (chunkSize - 1))))
						  .ToArray()
			).ConfigureAwait(false);

			// Logging: Chunks-Infos
			var chunkList = chunks.ToList();
			Debug.WriteLine($"[AggregateStretchedChunks] Chunks: {chunkList.Count}, ChunkSize: {chunkSize}, OutputLength: {outputLength}");
			for (int c = 0; c < Math.Min(3, chunkList.Count); c++)
			{
				var arr = chunkList[c];
				Debug.WriteLine($"Chunk[{c}] Length: {arr.Length}, Min: {arr.Min()}, Max: {arr.Max()}, First10: {string.Join(", ", arr.Take(10))}");
			}

			// Initialize accumulators in parallel
			double[] outputAccumulator = new double[outputLength];
			double[] weightSum = new double[outputLength];

			await Task.Run(() =>
			{
				var parallelOptions = new ParallelOptions
				{
					MaxDegreeOfParallelism = maxWorkers
				};

				// Phase 1: Process chunks in parallel
				Parallel.For(0, chunkList.Count, parallelOptions, chunkIndex =>
				{
					var chunk = chunkList[chunkIndex];
					int offset = chunkIndex * stretchedHopSize;

					for (int j = 0; j < Math.Min(chunkSize, chunk.Length); j++)
					{
						int idx = offset + j;
						if (idx >= outputLength)
						{
							break;
						}

						double windowedSample = chunk[j] * window[j];

						// Using Interlocked for thread-safe accumulation
						Interlocked.Exchange(ref outputAccumulator[idx], outputAccumulator[idx] + windowedSample);
						Interlocked.Exchange(ref weightSum[idx], weightSum[idx] + window[j]);
					}
				});

				// Phase 2: Normalize results
				float[] finalOutput = new float[outputLength];
				Parallel.For(0, outputLength, parallelOptions, i =>
				{
					finalOutput[i] = weightSum[i] > 1e-6
						? (float) (outputAccumulator[i] / weightSum[i])
						: 0.0f;
				});

				// Final assignment (thread-safe)
				this.Data = finalOutput;
			}).ConfigureAwait(true);

			// Logging: Output-Infos
			Debug.WriteLine($"[AggregateStretchedChunks] Output Min: {this.Data.Min()}, Max: {this.Data.Max()}, First10: {string.Join(", ", this.Data.Take(10))}");
		}

		public async Task EraseSelectionAsync()
		{
			if (this.Data == null || this.Data.Length == 0 || this.SelectionEnd < 0 || this.SelectionStart < 0 || this.SelectionStart == this.SelectionEnd)
			{
				return;
			}

			if (this.SelectionEnd < this.SelectionStart)
			{
				long swap = this.SelectionEnd;
				this.SelectionEnd = this.SelectionStart;
				this.SelectionStart = swap;
			}

			// Interleavte Samples (Floats), keine Frames
			long totalSamples = this.Data.LongLength;
			long selStart = Math.Clamp(this.SelectionStart, 0, totalSamples);
			long selEnd = Math.Clamp(this.SelectionEnd, 0, totalSamples);
			long selCount = selEnd - selStart;
			if (selCount <= 0)
			{
				return;
			}

			float[] newData = new float[this.Data.Length - selCount];
			await Task.Run(() =>
			{
				int bytesBefore = checked((int) (selStart * sizeof(float)));
				int srcAfterOffset = checked((int) (selEnd * sizeof(float)));
				int bytesAfter = checked((int) ((totalSamples - selEnd) * sizeof(float)));
				int dstAfterOffset = bytesBefore;

				if (bytesBefore > 0)
                {
                    Buffer.BlockCopy(this.Data, 0, newData, 0, bytesBefore);
                }

                if (bytesAfter > 0)
                {
                    Buffer.BlockCopy(this.Data, srcAfterOffset, newData, dstAfterOffset, bytesAfter);
                }
            });

			this.Data = newData;
			int channels = Math.Max(1, this.Channels);
			this.Length = this.Data.Length;
			this.Duration = TimeSpan.FromSeconds((double) this.Data.Length / (this.SampleRate * channels));

			this.SelectionStart = -1;
			this.SelectionEnd = -1;
		}

		// Playback Methods (umgestellt auf AudioPlaybackService)
		public async Task PlayAsync(CancellationToken cancellationToken, Action? onPlaybackStopped = null, float? initialVolume = null, int desiredLatency = 50)
		{
			this.Playing = true;
			this.Paused = false;
			initialVolume ??= this.Volume / 100f;

			if (this.Data == null || this.Data.Length == 0 || this.SampleRate <= 0 || this.Channels <= 0)
			{
				this.Playing = false;
				return;
			}

			try
			{
				// Start-Offset in Samples (interleaved) aus Byte-Offset berechnen
				int bytesPerFrame = Math.Max(1, this.Channels) * sizeof(float);
				long startSampleIndex = this.StartingOffset > 0 ? this.StartingOffset : 0;
				// Konsistente Ausgangsposition setzen (Bytes)
				long startFrames = Math.Max(0, this.StartingOffset > 0 ? this.StartingOffset / Math.Max(1, this.Channels) : 0);
				this.SkippedPositionBytes = startFrames * bytesPerFrame;

				// Player-Event binden (einmalig robust)
				EventHandler<StoppedEventArgs>? handler = null;
				handler = (sender, args) =>
				{
					try { onPlaybackStopped?.Invoke(); }
					finally
					{
						this.Playing = false;
						this.Paused = false;
						this.playback.PlaybackStopped -= handler!;
					}
				};
				this.playback.PlaybackStopped += handler;

				using (cancellationToken.Register(this.playback.Stop))
				{
					await this.playback.InitializePlayback(
						data: this.Data,
						sampleRate: this.SampleRate,
						channels: this.Channels,
						startSampleIndex: startSampleIndex,
						deviceSampleRate: this.SampleRate,
						desiredLatency: desiredLatency,
						initialVolume: initialVolume.Value);

					// Rate anwenden (Varispeed)
					if (Math.Abs(this.SampleRateFactor - 1.0) > double.Epsilon)
					{
						await this.playback.AdjustSampleRate((float) this.SampleRateFactor);
					}

					// Baseline setzen
					this.positionOriginBytes = this.playback.GetPositionBytes();
				}
			}
			catch (OperationCanceledException)
			{
				Debug.WriteLine("Playback preparation was canceled");
				this.Playing = false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Playback initialization failed: {ex.Message}");
				this.Playing = false;
				throw;
			}
		}

		public async Task PauseAsync()
		{
			if (this.PlayerPlaying)
			{
				long gp = 0;
				try { gp = this.playback.GetPositionBytes(); } catch { gp = 0; }
				long delta = gp - this.positionOriginBytes;
				if (delta < 0) { delta = 0; }
				long newAbsolute = this.SkippedPositionBytes + delta;
				int ch = Math.Max(1, this.Channels);
				int bytesPerFrame = ch * sizeof(float);
				long totalFrames = (this.Data?.LongLength ?? 0L) / ch;
				long totalBytes = totalFrames * bytesPerFrame;
				newAbsolute = Math.Clamp(newAbsolute, 0, totalBytes);
				this.SkippedPositionBytes = newAbsolute;
				this.playback.Pause();
				this.Playing = false;
				this.Paused = true;
				this.positionOriginBytes = gp;
				this.pausedBaselineBytes = this.SkippedPositionBytes; // remember start of pause
				this.resumeFromSetPosition = false;
				await Task.CompletedTask;
			}
			else if (this.Paused)
			{
				if (this.resumeFromSetPosition)
				{
					int ch = Math.Max(1, this.Channels);
					int bytesPerFrame = ch * sizeof(float);
					long startFrames = this.SkippedPositionBytes / bytesPerFrame;
					long startSampleIndex = startFrames * ch;
					// fast seek by swapping pipeline only
					this.playback.SeekSamples(startSampleIndex);
					// Jetzt wirklich wieder starten
					this.playback.Resume();
					this.Playing = true;
					this.Paused = false;
					try { this.positionOriginBytes = this.playback.GetPositionBytes(); } catch { this.positionOriginBytes = 0; }
					this.resumeFromSetPosition = false;
					await Task.CompletedTask;
					return;
				}
				try { this.positionOriginBytes = this.playback.GetPositionBytes(); } catch { this.positionOriginBytes = 0; }
				this.Playing = true;
				this.Paused = false;
				this.playback.Resume();
				await Task.CompletedTask;
			}
		}

		public async Task StopAsync()
		{
			this.Playing = false;
			this.Paused = false;

			this.playback.Stop();

			this.SkippedPositionBytes = 0;
			this.positionOriginBytes = 0;
			await Task.CompletedTask;
		}

		public void SetVolume(float volume)
		{
			volume = Math.Clamp(volume, 0.0f, 1.0f);
			this.Volume = volume * 100f;
			this.playback.SetVolume(volume);
		}

		// SetPosition: Frames -> Bytes; BasiseOffset setzen, ggf. nur merken (Seek beim nächsten Play)
		public void SetPosition(long framePosition)
		{
			int channels = Math.Max(1, this.Channels);
			int bytesPerFrame = channels * sizeof(float);
			long totalFrames = (this.Data?.LongLength ?? 0L) / channels;
			long totalBytes = totalFrames * bytesPerFrame;

			long bytePosition = framePosition * (long) bytesPerFrame;
			bytePosition = Math.Clamp(bytePosition, 0, totalBytes);

			this.SkippedPositionBytes = bytePosition;

			 // Minimal: Wenn pausiert, beim nächsten Resume neu starten
			if (this.Paused)
			{
				this.resumeFromSetPosition = true;
			}
		}

		public void Seek(double seconds)
		{
			long frames = (long) Math.Round(seconds * this.SampleRate);
			this.SetPosition(frames);
		}

		public async Task AdjustSampleRate(float factor)
		{
			this.SampleRateFactor = factor;

			if (this.PlayerPlaying)
			{
				await this.playback.AdjustSampleRate((float) this.SampleRateFactor);
			}
		}

		 // Seamless seek to absolute sample index. If playing, replace pipeline without stopping.
		public void JumpToSamples(long startSampleIndex)
		{
			int ch = Math.Max(1, this.Channels);
			if (this.Data == null || this.Data.LongLength <= 0 || ch <= 0)
			{
				return;
			}
			startSampleIndex = Math.Clamp(startSampleIndex, 0, this.Data.LongLength);
			if (this.PlayerPlaying)
			{
				try
				{
					this.playback.SeekSamples(startSampleIndex);
					int bytesPerFrame = ch * sizeof(float);
					long startFrames = startSampleIndex / ch;
					this.SkippedPositionBytes = startFrames * bytesPerFrame;
					try { this.positionOriginBytes = this.playback.GetPositionBytes(); } catch { this.positionOriginBytes = 0; }
				}
				catch { /* ignore */ }
			}
			else
			{
				long frames = startSampleIndex / ch;
				this.SetPosition(frames);
			}
		}

		// Processing (basic) Methods
		public async Task NormalizeAsync(float maxAmplitude = 1.0f, int maxWorkers = 4)
		{
			maxWorkers = Math.Clamp(maxWorkers, 1, Environment.ProcessorCount);

			if (this.Data == null || this.Data.Length == 0)
			{
				return;
			}

			var parallelOptions = new ParallelOptions
			{
				MaxDegreeOfParallelism = maxWorkers
			};

			Stopwatch sw = Stopwatch.StartNew();

			// Phase 1: Find global maximum (parallel + async)
			float globalMax = await Task.Run(() =>
			{
				float max = 0f;
				Parallel.For(0, this.Data.Length, parallelOptions,
					() => 0f,
					(i, _, localMax) => Math.Max(Math.Abs(this.Data[i]), localMax),
					localMax => { lock (this) { max = Math.Max(max, localMax); } }
				);
				return max;
			}).ConfigureAwait(false);

			if (globalMax == 0f)
			{
				return;
			}

			// Phase 2: Apply scaling (parallel + async)
			float scale = maxAmplitude / globalMax;
			await Task.Run(() =>
			{
				Parallel.For(0, this.Data.Length, parallelOptions, i =>
				{
					this.Data[i] *= scale;
				});
			}).ConfigureAwait(false);

			sw.Stop();
			this["Normalize"] = sw.Elapsed.TotalMilliseconds;
		}

		public async Task<(long StartIndex, long EndIndex)> TrimSilenceAsync(float? threshold = null, int maxWorkers = 4)
		{
			if (this.Data == null || this.Data.Length == 0)
			{
				return (0, 0);
			}

			maxWorkers = Math.Clamp(maxWorkers, 1, Environment.ProcessorCount);

			// Phase 1: RMS-Samples berechnen
			// Da wir interleaved Samples haben, ist das etwas komplizierter.
			// Wir berechnen die RMS über Blöcke von 10ms.
			int blockSize = (int) (this.SampleRate * this.Channels * 0.01); // 10 ms Blockgröße (interleaved samples)
			if (blockSize == 0)
			{
				blockSize = this.Channels;
			}

			// Buffergröße (Samples)
			int numBlocks = (int) Math.Ceiling((double) this.Data.Length / blockSize);
			float[] rmsBlocks = new float[numBlocks];

			await Task.Run(() =>
			{
				Parallel.For(0, numBlocks, new ParallelOptions { MaxDegreeOfParallelism = maxWorkers }, i =>
				{
					long start = (long) i * blockSize;
					long end = Math.Min(start + blockSize, this.Data.LongLength);

					double sumOfSquares = 0.0;
					long count = end - start;

					for (long s = start; s < end; s++)
					{
						sumOfSquares += this.Data[s] * this.Data[s];
					}

					rmsBlocks[i] = count > 0 ? (float) Math.Sqrt(sumOfSquares / count) : 0.0f;
				});
			}).ConfigureAwait(false);

			// Phase 2: Schwellenwert bestimmen
			float maxRms = rmsBlocks.Any() ? rmsBlocks.Max() : 0.0f;
			float finalThreshold = threshold ?? (maxRms * 0.01f); // 1% vom Max als Auto-Threshold

			// Phase 3: Start- und End-Block finden
			int startBlock = -1;
			for (int i = 0; i < rmsBlocks.Length; i++)
			{
				if (rmsBlocks[i] > finalThreshold)
				{
					startBlock = i;
					break;
				}
			}

			int endBlock = -1;
			for (int i = rmsBlocks.Length - 1; i >= 0; i--)
			{
				if (rmsBlocks[i] > finalThreshold)
				{
					endBlock = i;
					break;
				}
			}

			// Wenn nichts gefunden wird, gib die volle Länge zurück
			if (startBlock == -1 || endBlock == -1 || startBlock >= endBlock)
			{
				return (0, this.Data.LongLength);
			}

			// Ergebnis in interleaved Samples umrechnen
			long startIndex = (long) startBlock * blockSize;
			long endIndex = Math.Min((long) (endBlock + 1) * blockSize, this.Data.LongLength);

			return (startIndex, endIndex);
		}

		public async Task<float[]> ConvertToMonoAsync(bool set = false, int maxWorkers = 4)
		{
			maxWorkers = Math.Clamp(maxWorkers, 1, Environment.ProcessorCount);

			if (this.Data == null || this.Data.Length == 0 || this.Channels <= 0)
			{
				return [];
			}

			// Calculate the number of samples in mono
			int monoSampleCount = this.Data.Length / this.Channels;
			float[] monoData = new float[monoSampleCount];

			await Task.Run(() =>
			{
				Parallel.For(0, monoSampleCount, new ParallelOptions
				{
					MaxDegreeOfParallelism = maxWorkers
				}, i =>
				{
					float sum = 0.0f;
					for (int channel = 0; channel < this.Channels; channel++)
					{
						sum += this.Data[i * this.Channels + channel];
					}
					monoData[i] = sum / this.Channels;
				});
			});

			if (set)
			{
				this.Data = monoData;
				this.Channels = 1;
			}

			return monoData;
		}

		public async Task<float[]> GetCurrentWindowAsync(int windowSize = 65536, int lookingRange = 2, bool mono = false, bool lookBackwards = false)
		{
			if (this.Data == null || this.Data.Length == 0 || this.SampleRate <= 0 || this.Channels <= 0)
			{
				return [];
			}

			// Parameter normalisieren
			windowSize = Math.Max(1, windowSize);
			lookingRange = Math.Max(1, lookingRange);
			// Für spätere FFTs oft sinnvoll: auf Potenz von 2 schnappen
			windowSize = (int) Math.Pow(2, Math.Ceiling(Math.Log(windowSize, 2)));

			// Position in Frames (position ist bereits frame-basiert)
			long posFrames = this.Position;

			// Fensterbreite in Frames: um pos herum jeweils die Hälfte
			int halfWindowFrames = (windowSize * lookingRange) / 2;
			int fullWindowFrames = halfWindowFrames * 2;
			if (fullWindowFrames <= 0)
			{
				return [];
			}

			if (mono)
			{
				// Mono-Daten (Frames == Samples)
				float[] data = await this.ConvertToMonoAsync(false);
				if (data.Length == 0)
				{
					return [];
				}

				long startFrame = posFrames - (lookBackwards ? halfWindowFrames : 0);
				long endFrameExclusive = startFrame + fullWindowFrames;

				// Out-of-bounds vermeiden, verschieben
				while (endFrameExclusive > data.Length)
				{
					startFrame -= windowSize;
					endFrameExclusive -= windowSize;
				}

				while (startFrame < 0)
				{
					startFrame += windowSize;
					endFrameExclusive += windowSize;
				}

				if (endFrameExclusive > data.LongLength)
				{
					return [];
				}

				float[] current = new float[fullWindowFrames];
				await Task.Run(() => Array.Copy(data, (int) startFrame, current, 0, fullWindowFrames));
				return current;
			}
			else
			{
				// Interleaved Mehrkanal-Daten (Floats)
				float[] data = this.Data;

				long startFloatIndex = (posFrames - (lookBackwards ? halfWindowFrames : 0)) * this.Channels;
				long endFloatIndexExclusive = startFloatIndex + ((long) fullWindowFrames * this.Channels);

				while (endFloatIndexExclusive > data.Length)
				{
					startFloatIndex -= windowSize * this.Channels;
					endFloatIndexExclusive -= windowSize * this.Channels;
				}

				while (startFloatIndex < 0)
				{
					startFloatIndex += windowSize * this.Channels;
					endFloatIndexExclusive += windowSize * this.Channels;
				}

				if (endFloatIndexExclusive > data.LongLength || startFloatIndex < 0)
				{
					Debug.WriteLine("GetCurrentWindow: Out of bounds access prevented.");
					// Out-of-bounds, verschieben
					return [];
				}

				int lengthFloats = fullWindowFrames * this.Channels;
				float[] current = new float[lengthFloats];
				await Task.Run(() => Array.Copy(data, (int) startFloatIndex, current, 0, lengthFloats));
				return current;
			}
		}

		// Waveform Bitmap Methods
		[SupportedOSPlatform("windows")]
		public async Task<Bitmap> DrawWaveformAsync(int width, int height, int samplesPerPixel = 128, bool drawEachChannel = false, int caretWidth = 1, long? offset = null, Color? waveColor = null, Color? backColor = null, Color? caretColor = null, bool smoothen = false, double timingMarkersInterval = 0, float caretPosition = 0.0f, int maxWorkers = 2)
		{
			maxWorkers = Math.Clamp(maxWorkers, 1, Environment.ProcessorCount);
			waveColor ??= Color.Black;
			backColor ??= Color.White;
			caretColor ??= Color.Red;
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			samplesPerPixel = samplesPerPixel <= 0 ? this.CalculateSamplesPerPixelToFit(width) : samplesPerPixel;
			caretWidth = Math.Clamp(caretWidth, 0, width);

			offset ??= this.Position;

			long totalFrames = Math.Max(0, this.Length / Math.Max(1, this.Channels));
			long viewFrames = (long) width * samplesPerPixel;
			long maxOffset = Math.Max(0, totalFrames - viewFrames);
			if (offset.Value > maxOffset) { offset = maxOffset; }
			if (offset.Value < 0) { offset = 0; }

			var bitmap = new Bitmap(width, height);

			int channelsToDraw = drawEachChannel ? this.Channels : 1;
			var minMaxPerChannel = new (int yMin, int yMax)[channelsToDraw][];
			for (int c = 0; c < channelsToDraw; c++)
			{
				minMaxPerChannel[c] = new (int, int)[width];
			}

			// --- Performance tuning parameters ---
			// Ziel: maximal so viele samples pro pixel auswerten (budget). Wenn samplesPerPixel > budget => stride > 1.
			const int targetSamplesPerPixelBudget = 2048; // tweak: lower => more subsampling earlier; higher => more work
			int stride = Math.Max(1, (int) Math.Ceiling((double) samplesPerPixel / targetSamplesPerPixelBudget));
			// stride == 1 => full scan (wie vorher).
			// stride > 1 => wir lesen nur jedes stride-te Sample innerhalb des pixel-blocks.

			var data = this.Data; // assume float[] (interleaved channels)
			long dataLength = data?.LongLength ?? 0L;
			int channels = this.Channels;

			await Task.Run(() =>
			{
				var po = new ParallelOptions { MaxDegreeOfParallelism = maxWorkers };

				Parallel.For(0, channelsToDraw, po, channelIndex =>
				{
					try
					{
						int channelHeight = height / channelsToDraw;
						int centerY = channelHeight / 2 + channelIndex * channelHeight;

						// Compute once per channel
						long baseOffsetIndices = offset.Value * channels;

						for (int x = 0; x < width; x++)
						{
							long sampleStart = baseOffsetIndices + (long) x * samplesPerPixel * channels + channelIndex;

							// If no more data: fill rest with center line and break (cheap)
							if (sampleStart >= dataLength)
							{
								for (int xr = x; xr < width; xr++)
								{
									minMaxPerChannel[channelIndex][xr] = (centerY, centerY);
								}
								break;
							}

							long sampleEnd = Math.Min(sampleStart + (long) samplesPerPixel * channels, dataLength);

							// If stride > 1, step by channels*stride. Otherwise step by channels (original).
							long step = (long) channels * stride;

							// Fast local vars
							float min = float.MaxValue;
							float max = float.MinValue;

							// If stride is 1 and samplesPerPixel small, keep loop maybe vectorizable by JIT.
							// Using for-loop with index arithmetic rather than foreach.
							// Note: we ensure we don't step beyond sampleEnd.
							for (long idx = sampleStart; idx < sampleEnd; idx += step)
							{
								// idx is the position for this channel in interleaved buffer
								float sample = data![idx];
								if (sample < min)
								{
									min = sample;
								}

								if (sample > max)
								{
									max = sample;
								}
							}

							// If we subsampled (stride > 1) and the sampled block was very small (rare),
							// resulting min/max might remain float.Max/Min if no iteration happened.
							if (min == float.MaxValue && max == float.MinValue)
							{
								// fallback to center line
								min = 0f;
								max = 0f;
							}

							int yMin = centerY - (int) (min * (channelHeight / 2f));
							int yMax = centerY - (int) (max * (channelHeight / 2f));
							minMaxPerChannel[channelIndex][x] = (yMin, yMax);
						}
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"Fehler beim Berechnen der Waveform für Kanal {channelIndex}: {ex.Message}");
					}
				});
			}).ConfigureAwait(false);

			using (var g = Graphics.FromImage(bitmap))
			using (var pen = new Pen(waveColor.Value))
			{
				g.Clear(backColor.Value);

				long selStart = this.SelectionStart;
				long selEnd = this.SelectionEnd;
				if (this.Channels > 0 && selStart >= 0 && selEnd >= 0 && selStart != selEnd)
				{
					if (selEnd < selStart) { (selStart, selEnd) = (selEnd, selStart); }
					int ch = Math.Max(1, this.Channels);
					long selStartFrames = selStart / ch;
					long selEndFrames = selEnd / ch;
					long viewStartFrames = offset.Value;
					long viewEndFrames = viewStartFrames + viewFrames;
					long highlightStartFrames = Math.Max(viewStartFrames, selStartFrames);
					long highlightEndFrames = Math.Min(viewEndFrames, selEndFrames);
					if (highlightEndFrames > highlightStartFrames)
					{
						double invSPP = 1.0 / samplesPerPixel;
						int x1 = (int) Math.Floor((highlightStartFrames - viewStartFrames) * invSPP);
						int x2 = (int) Math.Ceiling((highlightEndFrames - viewStartFrames) * invSPP);
						int rectX = Math.Clamp(x1, 0, width);
						int rectW = Math.Clamp(x2 - rectX, 0, width - rectX);
						if (rectW > 0)
						{
							Color overlay = backColor.Value.GetBrightness() > 0.92f ? Color.FromArgb(28, 0, 0, 0) : Color.FromArgb(48, 255, 255, 255);
							using var selBrush = new SolidBrush(overlay);
							g.FillRectangle(selBrush, rectX, 0, rectW, height);
						}
					}
				}

				for (int channelIndex = 0; channelIndex < channelsToDraw; channelIndex++)
				{
					int channelHeight = height / channelsToDraw;
					int centerY = channelHeight / 2 + channelIndex * channelHeight;
					for (int x = 0; x < width; x++)
					{
						var (yMin, yMax) = minMaxPerChannel[channelIndex][x];
						// Fallback auf Grundlinie, falls (0,0) übrig blieb
						if (yMin == 0 && yMax == 0)
						{
							yMin = yMax = centerY;
						}
						if (samplesPerPixel == 1 && yMin == yMax && yMin != centerY)
						{
							yMax += 1; yMin -= 1;
						}
						g.DrawLine(pen, x, yMin, x, yMax);
					}
				}

				if (caretWidth > 0)
				{
					using var caretPen = new Pen(caretColor.Value, caretWidth);
					int caretX;
					if (caretPosition > 0.0f && caretPosition < 1.0f)
					{
						caretX = (int) Math.Round(caretPosition * (width - 1));
					}
					else
					{
						long pos = this.Position;
						caretX = (int) ((pos - offset.Value) / samplesPerPixel);
					}
					g.DrawLine(caretPen, caretX, 0, caretX, height);
				}
			}

			if (smoothen)
			{
				await Task.Run(() =>
				{
					var smoothBitmap = new Bitmap(width, height);
					using (var gSmooth = Graphics.FromImage(smoothBitmap))
					{
						gSmooth.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
						gSmooth.DrawImage(bitmap, new Rectangle(0, 0, width, height));
					}
					bitmap.Dispose();
					bitmap = smoothBitmap;
				}).ConfigureAwait(false);
			}

			if (timingMarkersInterval > 0)
			{
				Color inverseGraphColor = Color.FromArgb(255 - waveColor.Value.R, 255 - waveColor.Value.G, 255 - waveColor.Value.B);
				bitmap = await this.DrawTimingMarkersAsync(bitmap, samplesPerPixel, timingMarkersInterval, inverseGraphColor, false, offset.Value).ConfigureAwait(false);
			}

			return bitmap;
		}

		[SupportedOSPlatform("windows")]
		public async Task<Bitmap> DrawTimingMarkersAsync(Bitmap waveForm, int samplesPerPixel, double interval = 1, Color? color = null, bool drawTimes = false, long offsetFrames = 0)
		{
			color ??= Color.Gray;

			// Draw timing markers on existing bitmap considering current view offset & samplesPerPixel
			return await Task.Run(() =>
			{
				int width = waveForm.Width;
				int height = waveForm.Height;
				using (var g = Graphics.FromImage(waveForm))
				using (var pen = new Pen(color.Value))
				using (var font = new Font("Arial", 10))
				using (var brush = new SolidBrush(color.Value))
				{
					double invSPP = 1.0 / Math.Max(1, samplesPerPixel);
					long intervalFrames = (long) Math.Round(interval * Math.Max(1, this.SampleRate));
					if (intervalFrames <= 0)
					{
						return waveForm;
					}

					double intervalInPixels = intervalFrames * invSPP;
					// Erste Markierung relativ zum View-Start (offsetFrames)
					long remainder = offsetFrames % intervalFrames;
					double firstMarkerX = remainder == 0 ? 0.0 : (intervalFrames - remainder) * invSPP;
					for (double x = firstMarkerX; x < width; x += intervalInPixels)
					{
						if (x >= 0 && x < width)
						{
							g.DrawLine(pen, (float) x, 0, (float) x, height);

							if (drawTimes)
							{
								double seconds = (offsetFrames + (x * samplesPerPixel)) / (double) this.SampleRate;
								TimeSpan time = TimeSpan.FromSeconds(seconds);
								string timeLabel = time.ToString(@"mm\:ss");
								g.DrawString(timeLabel, font, brush, (float) x + 2, 2);
							}
						}
					}
				}
				return waveForm;
			}).ConfigureAwait(false);
		}

		// Info Methods
		public string GetInfoString(bool formatted = true)
		{
			List<string> infoLines = [
				$"{(this.SampleRate / 1000.0f):F1} Hz, {this.Channels} ch., {this.BitDepth} bits",
				$"Duration: {this.Duration:h\\:mm\\:ss\\.fff}",
				$"({this.Length} f32 ≙ {(this.SizeInKb / 1024.0f):F2} MB)",
				$"BPM-Tag: {this.Bpm:F3}",
				$"BPM Scanned: {this.ScannedBpm:F3}",
				];

			return formatted ? string.Join(Environment.NewLine, infoLines) : string.Join(" | ", infoLines);
		}

		public string GetMetricsString(bool formatted = true)
		{
			if (this.Metrics.Count <= 0)
			{
				return "No metrics available.";
			}
			List<string> metricLines = [];
			foreach (var kvp in this.Metrics)
			{
				metricLines.Add($"{kvp.Key}: {kvp.Value:F2} ms");
			}

			return formatted ? string.Join(Environment.NewLine, metricLines) : string.Join(" | ", metricLines);
		}

		// Private Methods
		private static IEnumerable<float> ReadAllSamplesStreaming(AudioFileReader reader)
		{
			const int BlockSeconds = 1;
			int blockSize = reader.WaveFormat.SampleRate * reader.WaveFormat.Channels * BlockSeconds;
			float[] buffer = new float[blockSize];
			int read;
			while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
			{
				for (int i = 0; i < read; i++)
				{
					yield return buffer[i];
				}
			}
		}

		private int CalculateSamplesPerPixelToFit(int width)
		{
			if (this.Data == null || this.Data.Length == 0 || width <= 0)
			{
				return 1;
			}
			int totalSamples = this.Data.Length / this.Channels;
			int samplesPerPixel = (int) Math.Ceiling((double) totalSamples / width);

			return Math.Max(1, samplesPerPixel);
		}
	}
}