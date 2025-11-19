using NAudience.Core;
using System;
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

            // Controls (Play symbol ?, Tag holds Stop ?) with template background
            var btnPlay = new Button { Size = this.button_playback.Size, Location = this.button_playback.Location, Text = "▶", Tag = "■", BackColor = this.button_playback.BackColor, FlatStyle = this.button_playback.FlatStyle };
            var btnPause = new Button { Size = this.button_pause.Size, Location = this.button_pause.Location, Text = "||", BackColor = this.button_pause.BackColor, FlatStyle = this.button_pause.FlatStyle };
            var btnLoop = new Button { Size = this.button_loop.Size, Location = this.button_loop.Location, Text = "↺", Font = this.button_loop.Font, BackColor = this.button_loop.BackColor, FlatStyle = this.button_loop.FlatStyle };
            var lblName = new Label { AutoSize = true, Location = this.label_name.Location, Text = audio.Name ?? "(unnamed)", BackColor = this.label_name.BackColor };
            var tbTime = new TextBox { ReadOnly = true, Size = this.textBox_time.Size, Location = this.textBox_time.Location, Text = "0:00:00.000", BackColor = this.textBox_time.BackColor, BorderStyle = this.textBox_time.BorderStyle };
            var pbWave = new PictureBox { BackColor = this.pictureBox_waveform.BackColor, Location = this.pictureBox_waveform.Location, Size = new Size(this.pictureBox_waveform.Width, this.TrackHeight - 42), Tag = audio.Id, BorderStyle = BorderStyle.None };
            var sbVol = new VScrollBar { Location = this.vScrollBar_volume.Location, Size = this.vScrollBar_volume.Size, Minimum = 0, Maximum = 1000, Value = 200 }; // 200/1000 => 80% (reversed mapping)
            var sbOffset = new HScrollBar { Location = new Point(this.hScrollBar_offset.Location.X, this.TrackHeight - 20), Size = this.hScrollBar_offset.Size, Minimum = 0, Maximum = 1 }; // will be updated
            var lblVol = new Label { AutoSize = true, Location = new Point(this.label_volume.Location.X, this.TrackHeight - 20), Font = this.label_volume.Font, Text = "80.0%", BackColor = this.label_volume.BackColor };

            panel.Controls.AddRange(new Control[] { btnPlay, btnPause, btnLoop, lblName, tbTime, pbWave, sbVol, sbOffset, lblVol });
            this.panel_waveforms.Controls.Add(panel);

            var tui = new TrackUi(audio, panel, pbWave, lblName, tbTime, btnPlay, btnPause, btnLoop, sbVol, sbOffset, lblVol)
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
            this.NAudience_Forms_WindowMainView_Tracks_UpdateScrollBar();
            this.LayoutTracks();
            this.ApplyVolumeFromScrollbar(tui); // initial 80%
            await this.RefreshWaveformAsync(tui); // erste Darstellung

            // Fit height if requested
            this.FitTracksToHeightIfEnabled();
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

        private ContextMenuStrip BuildTrackContextMenu(TrackUi tui)
        {
            var cms = new ContextMenuStrip();
            var miRename = new ToolStripMenuItem("Rename");
            var miCopy = new ToolStripMenuItem("Copy");
            var miExport = new ToolStripMenuItem("Export");
            var miDelete = new ToolStripMenuItem("Delete");
            var miAutoCut = new ToolStripMenuItem("Auto-Cut");
            var miAutoCutPalette = new ToolStripMenuItem("To Sample Palette");
            miAutoCutPalette.Click += async (_, __) => await this.AutoCutToPaletteAsync(tui);
            miAutoCut.DropDownItems.Add(miAutoCutPalette);

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

            cms.Items.AddRange(new ToolStripItem[] { miRename, miCopy, miExport, miAutoCut, miDelete });
            return cms;
        }

        private async Task AutoCutToPaletteAsync(TrackUi tui)
        {
            try
            {
                var audio = tui.Audio;
                if (audio == null || audio.Data == null || audio.Data.Length == 0)
                {
                    return;
                }
                // Use collection settings
                var cuts = await audio.AutoCutAsync(
                    threshold: this.AudioC.Threshold,
                    minDurationMs: this.AudioC.MinDurationMs,
                    maxDurationMs: this.AudioC.MaxDurationMs,
                    silenceWindowMs: this.AudioC.SilenceWindowMs,
                    mergeSimilarThreshold: null,
                    onePaletteLoop: true);
                if (cuts != null && cuts.Count > 0)
                {
                    foreach (var newAudio in cuts)
                    {
                        this.AudioC.Audios.Add(newAudio);
                    }
                }
            }
            catch (Exception ex)
            {
                try { LogCollection.Log(ex); } catch { }
                MessageBox.Show("Auto-Cut failed: " + ex.Message, "Auto-Cut", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            public Label LabelVolume { get; }
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

            public TrackUi(AudioObj audio, Panel panel, PictureBox wave, Label name, TextBox time, Button play, Button pause, Button loop, VScrollBar vol, HScrollBar offs, Label lblVol)
            {
                this.Audio = audio; this.Panel = panel; this.PictureWave = wave; this.LabelName = name; this.TextTime = time; this.ButtonPlay = play; this.ButtonPause = pause; this.ButtonLoop = loop; this.ScrollVolume = vol; this.ScrollOffset = offs; this.LabelVolume = lblVol;
            }
        }
    }
}
