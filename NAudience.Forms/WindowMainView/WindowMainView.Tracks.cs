using NAudience.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NAudience.Forms
{
	public partial class WindowMainView
	{
		private const int CaretAnchorPx = 24; // fixed caret anchor (left side)
		private static readonly int[] LoopSteps = { 1, 2, 4, 8, 16, 32, 64 };
		private const int DragThresholdPx = 4;

		private void InitializeTrackTemplate()
		{
			// Template unsichtbar & weit weg platzieren
			this.panel_track.Visible = false;
			this.panel_track.Enabled = false;
			this.panel_track.Location = new Point(-5000, -5000);

			// Scrollbar events
			this.vScrollBar_tracks.Scroll += (_, __) => this.LayoutTracks();
		}

		private async Task BuildTrackAsync(AudioObj audio)
		{
			if (audio == null)
			{
				return;
			}

			var panel = new Panel
			{
				BackColor = this.panel_track.BackColor,
				Size = new Size(this.panel_track.Width, this.TrackHeight),
				Margin = new Padding(0),
				Tag = audio.Id
			};

			// Controls (Play symbol ▶, Tag holds Stop ■)
			var btnPlay = new Button { Size = this.button_playback.Size, Location = this.button_playback.Location, Text = "▶", Tag = "■" };
			var btnPause = new Button { Size = this.button_pause.Size, Location = this.button_pause.Location, Text = "||" };
			var btnLoop = new Button { Size = this.button_loop.Size, Location = this.button_loop.Location, Text = "↺", Font = this.button_loop.Font };
			var lblName = new Label { AutoSize = true, Location = this.label_name.Location, Text = audio.Name ?? "(unnamed)" };
			var tbTime = new TextBox { ReadOnly = true, Size = this.textBox_time.Size, Location = this.textBox_time.Location, Text = "0:00:00.000" };
			var pbWave = new PictureBox { BackColor = this.pictureBox_waveform.BackColor, Location = this.pictureBox_waveform.Location, Size = new Size(this.pictureBox_waveform.Width, this.TrackHeight - 42), Tag = audio.Id, BorderStyle = BorderStyle.None };
			var sbVol = new VScrollBar { Location = this.vScrollBar_volume.Location, Size = this.vScrollBar_volume.Size, Minimum = 0, Maximum = 100, Value = 20 }; // 20 => 80% (reversed mapping)
			var sbOffset = new HScrollBar { Location = new Point(this.hScrollBar_offset.Location.X, this.TrackHeight - 20), Size = this.hScrollBar_offset.Size, Minimum = 0, Maximum = 1 }; // will be updated

			panel.Controls.AddRange(new Control[] { btnPlay, btnPause, btnLoop, lblName, tbTime, pbWave, sbVol, sbOffset });
			this.panel_waveforms.Controls.Add(panel);

			var tui = new TrackUi(audio, panel, pbWave, lblName, tbTime, btnPlay, btnPause, btnLoop, sbVol, sbOffset)
			{
				SamplesPerPixel = 128,
				OffsetFrames = 0
			};
			this.trackUis[audio.Id] = tui;

			// Events
			btnPlay.Click += async (_, __) => await this.TogglePlayAsync(tui);
			btnPause.Click += async (_, __) => await this.TogglePauseAsync(tui);
			btnLoop.Click += (_, e) => this.ToggleLoop(tui, e as MouseEventArgs);
			pbWave.MouseDown += (_, e) => this.Wave_MouseDown(tui, e);
			pbWave.MouseMove += (_, e) => { this.Wave_MouseMove(tui, e); tui.MouseOver = true; tui.MouseX = e.X; };
			pbWave.MouseUp += (_, e) => this.Wave_MouseUp(tui, e);
			pbWave.MouseWheel += (_, e) => this.Wave_MouseWheel(tui, e);
			pbWave.MouseEnter += (_, __) => { tui.MouseOver = true; };
			pbWave.MouseLeave += (_, __) => { tui.MouseOver = false; };
			sbVol.Scroll += (_, __) => this.ApplyVolumeFromScrollbar(tui);
			sbOffset.Scroll += (_, __) => this.OffsetScrollbarScrolled(tui);

			// Kontextmenü
			panel.ContextMenuStrip = this.BuildTrackContextMenu(tui);
			pbWave.ContextMenuStrip = panel.ContextMenuStrip;

			this.UpdateOffsetScrollbar(tui);
			this.UpdateScrollBar();
			this.LayoutTracks();
			this.ApplyVolumeFromScrollbar(tui); // initial 80%
			await this.RefreshWaveformAsync(tui); // erste Darstellung
		}

		private void ApplyVolumeFromScrollbar(TrackUi tui)
		{
			float vol = 1f - (tui.ScrollVolume.Value / (float)tui.ScrollVolume.Maximum); // reversed
			vol = Math.Clamp(vol, 0f, 1f);
			try { tui.Audio.SetVolume(vol); } catch { }
		}

		private void OffsetScrollbarScrolled(TrackUi tui)
		{
			if (tui.Audio.Playing)
			{
				return; // manuell nur wenn gestoppt/pausiert
			}

			long newOffset = tui.ScrollOffset.Value;
			// Map scrollbar value to frame offset
			long maxOffset = this.GetMaxOffsetFrames(tui);
			if (maxOffset <= 0) { tui.OffsetFrames = 0; } else { tui.OffsetFrames = Math.Min(maxOffset, newOffset); }
			_ = this.RefreshWaveformAsync(tui);
		}

		private long GetTotalFrames(AudioObj audio)
		{
			int ch = Math.Max(1, audio.Channels);
			long totalSamples = audio.Length; // assumed interleaved sample count
			return totalSamples / ch; // frames
		}

		private long GetMaxOffsetFrames(TrackUi tui)
		{
			long totalFrames = this.GetTotalFrames(tui.Audio);
			// SamplesPerPixel represents FRAMES per pixel -> visibleFrames in frames
			long visibleFrames = (long)tui.PictureWave.Width * tui.SamplesPerPixel;
			return Math.Max(0, totalFrames - visibleFrames);
		}

		private void UpdateOffsetScrollbar(TrackUi tui)
		{
			long maxOffset = this.GetMaxOffsetFrames(tui);
			long visibleFrames = (long)tui.PictureWave.Width * tui.SamplesPerPixel;
			int sbMax = (int)Math.Min(int.MaxValue - 1, maxOffset);
			int large = (int)Math.Min(int.MaxValue / 8, Math.Max(1, visibleFrames));
			int small = Math.Max(1, large / 10);
			var sb = tui.ScrollOffset;
			sb.Minimum = 0;
			sb.LargeChange = large;
			sb.SmallChange = small;
			sb.Maximum = sbMax + sb.LargeChange; // WinForms quirk
			sb.Enabled = sbMax > 0;
			sb.Value = (int)Math.Min(sbMax, tui.OffsetFrames);
		}

		private void Wave_MouseWheel(TrackUi tui, MouseEventArgs e)
		{
			if ((ModifierKeys & Keys.Control) != 0)
			{
				int old = tui.SamplesPerPixel;
				if (e.Delta > 0)
				{
					int step = Math.Max(1, old / 8);
					int nw = Math.Max(8, old - step);
					tui.SamplesPerPixel = nw;
				}
				else
				{
					int step = Math.Max(1, old / 6);
					int nw = Math.Min(16384, old + step);
					tui.SamplesPerPixel = nw;
				}
				this.UpdateOffsetScrollbar(tui);
				_ = this.RefreshWaveformAsync(tui);
			}
			else
			{
				if (tui.Audio.Playing) return;
				long visibleFrames = (long)tui.PictureWave.Width * tui.SamplesPerPixel;
				long stepFrames = Math.Max(1, (long)(visibleFrames * 0.15));
				if (e.Delta > 0)
				{
					tui.OffsetFrames = Math.Max(0, tui.OffsetFrames - stepFrames);
				}
				else
				{
					long maxOffset = this.GetMaxOffsetFrames(tui);
					tui.OffsetFrames = Math.Min(maxOffset, tui.OffsetFrames + stepFrames);
				}
				this.UpdateOffsetScrollbar(tui);
				_ = this.RefreshWaveformAsync(tui);
			}
		}

		private void RebuildAllTracks()
		{
			foreach (var kv in this.trackUis)
			{
				kv.Value.Panel.Dispose();
			}
			this.trackUis.Clear();
			foreach (var audio in this.AudioC.Audios)
			{
				_ = this.BuildTrackAsync(audio);
			}
		}

		private void LayoutTracks()
		{
			int scrollY = this.vScrollBar_tracks.Value;
			int index = 0;
			foreach (var audio in this.AudioC.Audios)
			{
				if (!this.trackUis.TryGetValue(audio.Id, out var tui))
				{
					continue;
				}

				int y = index * this.TrackHeight - scrollY;
				tui.Panel.Location = new Point(3, y + 3);
				index++;
			}
		}

		private void UpdateScrollBar()
		{
			int totalHeight = this.AudioC.Count * this.TrackHeight;
			this.vScrollBar_tracks.Minimum = 0;
			this.vScrollBar_tracks.Maximum = Math.Max(0, totalHeight);
			this.vScrollBar_tracks.LargeChange = this.panel_waveforms.Height;
			this.vScrollBar_tracks.SmallChange = this.TrackHeight / 2;
		}

		private ContextMenuStrip BuildTrackContextMenu(TrackUi tui)
		{
			var cms = new ContextMenuStrip();
			var miRename = new ToolStripMenuItem("Rename");
			var miCopy = new ToolStripMenuItem("Copy");
			var miExport = new ToolStripMenuItem("Export");
			var miDelete = new ToolStripMenuItem("Delete");

			miRename.Click += (_, __) => this.RenameTrack(tui);
			miCopy.Click += async (_, __) => await this.CopyTrackAsync(tui);
			miDelete.Click += async (_, __) => await this.DeleteTrackAsync(tui);

			// Export Untermenüs
			var wav = new ToolStripMenuItem("WAV");
			foreach (int bits in new[] { 8, 16, 24 })
			{
				var item = new ToolStripMenuItem(bits + " bit");
				item.Click += async (_, __) => await this.ExportTrackAsync(tui, ".wav", bits);
				wav.DropDownItems.Add(item);
			}
			var mp3 = new ToolStripMenuItem("MP3");
			foreach (int br in new[] { 64, 96, 128, 192, 256, 320 })
			{
				var item = new ToolStripMenuItem(br + " kbps");
				item.Click += async (_, __) => await this.ExportTrackAsync(tui, ".mp3", br);
				mp3.DropDownItems.Add(item);
			}
			miExport.DropDownItems.Add(wav);
			miExport.DropDownItems.Add(mp3);

			cms.Items.AddRange(new ToolStripItem[] { miRename, miCopy, miExport, miDelete });
			return cms;
		}

		private void RenameTrack(TrackUi tui)
		{
			string? input = Microsoft.VisualBasic.Interaction.InputBox("Track name", "Rename Track", tui.Audio.Name);
			if (!string.IsNullOrWhiteSpace(input))
			{
				try { tui.Audio.Name = input; tui.LabelName.Text = input; } catch { }
			}
		}

		private async Task CopyTrackAsync(TrackUi tui)
		{
			AudioObj clone;
			bool hasSelection = tui.Audio.SelectionStart >= 0 && tui.Audio.SelectionEnd > tui.Audio.SelectionStart;
			if (hasSelection)
			{
				var selClone = await tui.Audio.CloneFromSelectionAsync();
				clone = selClone ?? await tui.Audio.CloneAsync();
			}
			else
			{
				clone = await tui.Audio.CloneAsync();
			}
			this.AudioC.Audios.Add(clone);
		}

		private async Task DeleteTrackAsync(TrackUi tui)
		{
			await this.AudioC.RemoveAsync(tui.Audio.Id);
			this.UpdateScrollBar();
			this.LayoutTracks();
		}

		private Task ExportTrackAsync(TrackUi tui, string format, int bitsOrBitrate)
		{
			return this.AudioC.ExportAsync(tui.Audio.Id, format, bitsOrBitrate);
		}

		private async Task TogglePlayAsync(TrackUi tui)
		{
			var audio = tui.Audio;
			if (!audio.Playing)
			{
				long startFrame = 0;
				if (audio.SelectionStart >= 0 && audio.SelectionEnd > audio.SelectionStart)
				{
					startFrame = audio.SelectionStart / Math.Max(1, audio.Channels); // selection positions in samples -> frames
				}
				else if (tui.LastClickFrame >= 0)
				{
					startFrame = tui.LastClickFrame;
				}
				audio.SetPosition(startFrame);
				float initialVol = 1f - (tui.ScrollVolume.Value / (float)tui.ScrollVolume.Maximum);
				await audio.PlayAsync(CancellationToken.None, null, initialVol);
				if (tui.ButtonPlay.Tag is string stopSymbol && !string.IsNullOrWhiteSpace(stopSymbol))
				{
					tui.ButtonPlay.Text = stopSymbol; // ■
				}
			}
			else
			{
				await audio.StopAsync();
				tui.ButtonPlay.Text = "▶";
			}
		}

		private async Task TogglePauseAsync(TrackUi tui)
		{
			var audio = tui.Audio;
			if (audio.Playing || audio.Paused)
			{
				await audio.PauseAsync();
			}
			else
			{
				await audio.PlayAsync(CancellationToken.None);
			}
		}

		private void ToggleLoop(TrackUi tui, MouseEventArgs? e)
		{
			if (e != null && (ModifierKeys & Keys.Control) != 0)
			{
				// Disable
				tui.LoopEnabled = false;
				tui.LoopDenominator = 0;
				tui.ButtonLoop.Text = "↺";
				tui.ButtonLoop.ForeColor = Color.Black;

				// Segoe UI Symbol Font 9pt Bold
				tui.ButtonLoop.Font = new Font("Segoe UI Symbol", 9f, FontStyle.Bold);
				return;
			}

			int dir = (e != null && (ModifierKeys & Keys.Shift) != 0) ? -1 : 1;
			if (!tui.LoopEnabled)
			{
				tui.LoopEnabled = true;
				tui.ButtonLoop.Font = new Font("Segoe UI Symbol", 7.5f, FontStyle.Bold);
				tui.LoopDenominator = 1;
			}
			else
			{
				int idx = Array.IndexOf(LoopSteps, tui.LoopDenominator);
				if (idx < 0) idx = 0;
				idx = (idx + dir) % LoopSteps.Length;
				if (idx < 0) idx += LoopSteps.Length;
				tui.LoopDenominator = LoopSteps[idx];
			}
			this.RecalculateLoopFraction(tui);
			tui.ButtonLoop.Text = tui.LoopDenominator.ToString();
			tui.ButtonLoop.ForeColor = Color.Green;
		}

		private void RecalculateLoopFraction(TrackUi tui)
		{
			if (!tui.LoopEnabled || tui.LoopDenominator <= 0)
			{
				tui.LoopBaseStartSamples = 0;
				tui.LoopBaseEndSamples = 0;
				tui.LoopFractionSamples = 0;
				return;
			}
			long totalSamples = tui.Audio.Length;
			long regionStart = 0;
			long regionEnd = totalSamples;
			if (tui.Audio.SelectionStart >= 0 && tui.Audio.SelectionEnd > tui.Audio.SelectionStart)
			{
				regionStart = tui.Audio.SelectionStart;
				regionEnd = Math.Min(totalSamples, tui.Audio.SelectionEnd);
			}
			long regionLen = Math.Max(1, regionEnd - regionStart);
			long fraction = regionLen / tui.LoopDenominator;
			fraction = Math.Max(1, fraction);
			tui.LoopBaseStartSamples = regionStart;
			tui.LoopBaseEndSamples = regionEnd;
			tui.LoopFractionSamples = fraction;
		}

		private void Wave_MouseDown(TrackUi tui, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				// Cancel selection if currently selecting
				tui.DragSelecting = false;
				tui.PendingSelect = false;
				return;
			}

			if (e.Button == MouseButtons.Left)
			{
				long frame = this.MapPixelToFrameInView(tui, e.X);
				tui.MouseDownX = e.X;
				tui.PendingSelect = true;
				tui.DragSelecting = false;
				tui.SelectStartFrame = frame;
				tui.SelectEndFrame = frame;
				tui.LastClickFrame = frame;
			}
		}

		private void Wave_MouseMove(TrackUi tui, MouseEventArgs e)
		{
			if (tui.PendingSelect && !tui.DragSelecting)
			{
				if (Math.Abs(e.X - tui.MouseDownX) >= DragThresholdPx)
				{
					// start selecting
					tui.DragSelecting = true;
				}
			}

			if (tui.DragSelecting)
			{
				long frame = this.MapPixelToFrameInView(tui, e.X);
				tui.SelectEndFrame = frame;
				this.UpdateSelection(tui);
				_ = this.RefreshWaveformAsync(tui);
			}
		}

		private void Wave_MouseUp(TrackUi tui, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				if (!tui.DragSelecting && tui.PendingSelect)
				{
					// treat as click: set starting position, no selection
					long frame = this.MapPixelToFrameInView(tui, e.X);
					int ch = Math.Max(1, tui.Audio.Channels);
					tui.Audio.SelectionStart = -1;
					tui.Audio.SelectionEnd = -1;
					tui.Audio.SetPosition(frame);
					tui.Audio.StartingOffset = frame * ch; // in samples
					// align view so caret anchor shows that frame
					long desiredOffset = frame - (long)CaretAnchorPx * tui.SamplesPerPixel;
					desiredOffset = Math.Max(0, desiredOffset);
					long maxOffset = this.GetMaxOffsetFrames(tui);
					tui.OffsetFrames = Math.Min(maxOffset, desiredOffset);
					this.UpdateOffsetScrollbar(tui);
					_ = this.RefreshWaveformAsync(tui);
				}
				else if (tui.DragSelecting)
				{
					// finalize selection
					long frame = this.MapPixelToFrameInView(tui, e.X);
					tui.SelectEndFrame = frame;
					this.UpdateSelection(tui);
					_ = this.RefreshWaveformAsync(tui);
				}
				// reset flags
				tui.PendingSelect = false;
				tui.DragSelecting = false;
			}
		}

		private void UpdateSelection(TrackUi tui)
		{
			long start = Math.Min(tui.SelectStartFrame, tui.SelectEndFrame);
			long end = Math.Max(tui.SelectStartFrame, tui.SelectEndFrame);
			int channels = Math.Max(1, tui.Audio.Channels);
			if (end - start > 0)
			{
				long sampleStart = start * channels;
				long sampleEnd = end * channels;
				tui.Audio.SelectionStart = sampleStart;
				tui.Audio.SelectionEnd = sampleEnd;
			}
			else
			{
				tui.Audio.SelectionStart = -1;
				tui.Audio.SelectionEnd = -1;
			}
			this.RecalculateLoopFraction(tui); // update loop region if active
		}

		private long MapPixelToFrame(AudioObj audio, int pixelX, int width)
		{
			pixelX = Math.Clamp(pixelX, 0, width);
			long totalFrames = audio.Length / Math.Max(1, audio.Channels);
			if (totalFrames <= 0) return 0;
			return (long)(totalFrames * (pixelX / (double)width));
		}

		private long MapPixelToFrameInView(TrackUi tui, int pixelX)
		{
			pixelX = Math.Clamp(pixelX, 0, tui.PictureWave.Width);
			long frame = tui.OffsetFrames + (long)pixelX * tui.SamplesPerPixel;
			long totalFrames = this.GetTotalFrames(tui.Audio);
			return Math.Clamp(frame, 0, Math.Max(0, totalFrames));
		}

		private async Task RefreshWaveformAsync(TrackUi tui)
		{
			try
			{
				int channels = Math.Max(1, tui.Audio.Channels);
				long totalSamples = tui.Audio.Length;
				long totalFrames = totalSamples / channels;
				long posFrames = Math.Max(0, tui.Audio.Position);
				// Auto-scroll during playback keeping caret fixed at CaretAnchorPx until end of scroll range
				if (tui.Audio.Playing)
				{
					long desiredOffset = posFrames - (long)CaretAnchorPx * tui.SamplesPerPixel;
					desiredOffset = Math.Max(0, desiredOffset);
					long maxOffset = this.GetMaxOffsetFrames(tui);
					if (desiredOffset > maxOffset) desiredOffset = maxOffset;
					if (desiredOffset != tui.OffsetFrames)
					{
						tui.OffsetFrames = desiredOffset;
						this.UpdateOffsetScrollbar(tui);
					}
				}
				long offsetFrames = tui.OffsetFrames;
					// Caret position: anchored unless we're at the end of scroll range
				float caretPosNorm;
				long maxOff = this.GetMaxOffsetFrames(tui);
				if (offsetFrames >= maxOff)
				{
					// caret depends on actual position inside the last window
					double px = (posFrames - offsetFrames) / (double)Math.Max(1, tui.SamplesPerPixel);
					caretPosNorm = (float)Math.Clamp(px / Math.Max(1, tui.PictureWave.Width - 1), 0.0, 1.0);
				}
				else
				{
					caretPosNorm = Math.Clamp(CaretAnchorPx / (float)Math.Max(1, tui.PictureWave.Width - 1), 0f, 1f);
				}
				var bmp = await tui.Audio.DrawWaveformAsync(tui.PictureWave.Width, tui.PictureWave.Height,
					 samplesPerPixel: tui.SamplesPerPixel, waveColor: this.ColorWave, backColor: this.ColorBack, caretColor: this.ColorCaret,
					 caretPosition: caretPosNorm, offset: offsetFrames);

				// Selection overlay inside visible range using frames
				if (tui.Audio.SelectionStart >= 0 && tui.Audio.SelectionEnd > tui.Audio.SelectionStart)
				{
					using var g = Graphics.FromImage(bmp);
					long visStartFrames = offsetFrames;
					long visEndFrames = visStartFrames + (long)tui.PictureWave.Width * tui.SamplesPerPixel;
					long selStartFrames = tui.Audio.SelectionStart / channels;
					long selEndFrames = tui.Audio.SelectionEnd / channels;
					long drawStart = Math.Max(selStartFrames, visStartFrames);
					long drawEnd = Math.Min(selEndFrames, visEndFrames);
					if (drawEnd > drawStart)
					{
						float pxStart = (drawStart - visStartFrames) / (float)tui.SamplesPerPixel;
						float pxEnd = (drawEnd - visStartFrames) / (float)tui.SamplesPerPixel;
						pxStart = Math.Clamp(pxStart, 0, bmp.Width);
						pxEnd = Math.Clamp(pxEnd, 0, bmp.Width);
						var rect = new RectangleF(pxStart, 0, pxEnd - pxStart, bmp.Height);
						using var brush = new SolidBrush(Color.FromArgb(80, this.ColorSelection));
						g.FillRectangle(brush, rect);
					}
				}
				var old = tui.PictureWave.Image;
				tui.PictureWave.Image = bmp;
				old?.Dispose();
			}
			catch (Exception ex)
			{
				LogCollection.Log(ex);
			}
		}

		private sealed class TrackUi
		{
			public AudioObj Audio { get; }
			public Panel Panel { get; }
			public PictureBox PictureWave { get; }
			public Label LabelName { get; }
			public TextBox TextTime { get; }
			public Button ButtonPlay { get; }
			public Button ButtonPause { get; }
			public Button ButtonLoop { get; }
			public VScrollBar ScrollVolume { get; }
			public HScrollBar ScrollOffset { get; }
			public bool LoopEnabled { get; set; }
			public int LoopDenominator { get; set; }
			public long SelectStartFrame { get; set; } = -1;
			public long SelectEndFrame { get; set; } = -1;
			public bool DragSelecting { get; set; }
			public bool PendingSelect { get; set; }
			public int MouseDownX { get; set; }
			public long LastClickFrame { get; set; } = -1;
			public int SamplesPerPixel { get; set; } = 128;
			public long OffsetFrames { get; set; } = 0;
			public long LoopBaseStartSamples { get; set; } = 0;
			public long LoopBaseEndSamples { get; set; } = 0;
			public long LoopFractionSamples { get; set; } = 0;
			public bool MouseOver { get; set; }
			public int MouseX { get; set; }

			public TrackUi(AudioObj audio, Panel panel, PictureBox wave, Label name, TextBox time, Button play, Button pause, Button loop, VScrollBar vol, HScrollBar offs)
			{
				this.Audio = audio; this.Panel = panel; this.PictureWave = wave; this.LabelName = name; this.TextTime = time; this.ButtonPlay = play; this.ButtonPause = pause; this.ButtonLoop = loop; this.ScrollVolume = vol; this.ScrollOffset = offs;
			}
		}
	}
}
