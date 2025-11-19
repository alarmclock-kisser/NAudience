using NAudience.Core;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NAudience.Forms
{
    public partial class WindowMainView
    {
        private void ApplyVolumeFromScrollbar(TrackUi tui)
        {
            float vol = 1f - (tui.ScrollVolume.Value / (float) tui.ScrollVolume.Maximum); // reversed
            vol = Math.Clamp(vol, 0f, 1f);
            tui.LabelVolume.Text = (vol * 100f).ToString("F1") + "%";
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
                long visibleFrames = (long) tui.PictureWave.Width * tui.SamplesPerPixel;
                long stepFrames = Math.Max(1, (long) (visibleFrames * 0.15));
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

        private static void ApplyLoopFractionToAudio(TrackUi tui)
        {
            var audio = tui.Audio;
            long total = audio.Length;
            long baseStart = (audio.SelectionStart >= 0 && audio.SelectionEnd > audio.SelectionStart) ? audio.SelectionStart : 0;
            long baseEnd = (audio.SelectionStart >= 0 && audio.SelectionEnd > audio.SelectionStart) ? Math.Min(total, audio.SelectionEnd) : total;
            long fraction = (tui.LoopEnabled && tui.LoopDenominator > 0) ? Math.Max(1, (baseEnd - baseStart) / tui.LoopDenominator) : 0;
            bool adjust = true;
            audio.UpdateLoopFraction(baseStart, baseEnd, fraction, loopEnabled: tui.LoopEnabled, adjustPosition: adjust);
        }

        private async Task TogglePlayAsync(TrackUi tui)
        {
            var audio = tui.Audio;
            if (audio.Paused)
            {
                // Resume from paused position (no re-init)
                await audio.PauseAsync(); // second call resumes
                tui.ButtonPlay.Text = "■"; // show stop symbol
                return;
            }
            if (!audio.Playing)
            {
                // ensure loop state and fraction are applied to audio selection before starting fresh
                ApplyLoopFractionToAudio(tui);
                long startFrame = 0;
                // Prefer loop/selection start when loop enabled
                if (tui.LoopEnabled && (audio.SelectionStart >= 0 && audio.SelectionEnd > audio.SelectionStart))
                {
                    startFrame = audio.SelectionStart / Math.Max(1, audio.Channels);
                }
                else if (audio.StartingOffset > 0)
                {
                    startFrame = audio.StartingOffset / Math.Max(1, audio.Channels);
                }
                else if (tui.LastClickFrame >= 0)
                {
                    startFrame = tui.LastClickFrame;
                }
                audio.SetPosition(startFrame);
                float initialVol = 1f - (tui.ScrollVolume.Value / (float)tui.ScrollVolume.Maximum);

                // Ensure UI is updated when playback stops (thread-safe)
                Action onStopped = () =>
                {
                    try
                    {
                        if (!tui.ButtonPlay.IsDisposed && tui.ButtonPlay.IsHandleCreated)
                        {
                            if (tui.ButtonPlay.InvokeRequired)
                            {
                                tui.ButtonPlay.BeginInvoke(new Action(() => tui.ButtonPlay.Text = "▶"));
                            }
                            else
                            {
                                tui.ButtonPlay.Text = "▶";
                            }
                        }
                    }
                    catch { }
                };

                await audio.PlayAsync(CancellationToken.None, onStopped, initialVol);
                // Update button to Stop symbol
                tui.ButtonPlay.Text = "■";
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
                // After pausing, set Play button to play symbol
                if (audio.Paused)
                {
                    tui.ButtonPlay.Text = "▶";
                }
            }
            else
            {
                // starting from stopped state via pause button -> treat like play
                ApplyLoopFractionToAudio(tui);

                Action onStopped = () =>
                {
                    try
                    {
                        if (!tui.ButtonPlay.IsDisposed && tui.ButtonPlay.IsHandleCreated)
                        {
                            if (tui.ButtonPlay.InvokeRequired)
                            {
                                tui.ButtonPlay.BeginInvoke(new Action(() => tui.ButtonPlay.Text = "▶"));
                            }
                            else
                            {
                                tui.ButtonPlay.Text = "▶";
                            }
                        }
                    }
                    catch { }
                };

                await audio.PlayAsync(CancellationToken.None, onStopped);
                tui.ButtonPlay.Text = "■";
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
                // propagate to audio
                tui.Audio.LoopEnabled = tui.LoopEnabled;
                // update loop immediately
                ApplyLoopFractionToAudio(tui);
                // align view & caret with current position after leaving loop
                this.AlignViewToCurrentPosition(tui);
                _ = this.RefreshWaveformAsync(tui);
                return;
            }

            int dir = (e != null && (ModifierKeys & Keys.Shift) != 0) ? -1 : 1;
            if (!tui.LoopEnabled)
            {
                tui.LoopEnabled = true;
                tui.ButtonLoop.Font = new Font("Segoe UI Symbol", 7f, FontStyle.Bold);
                tui.LoopDenominator = 1;
                tui.ButtonLoop.Text = tui.LoopDenominator.ToString();
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
            // propagate to audio and update immediately
            ApplyLoopFractionToAudio(tui);
            ForceJumpIntoLoopIfNeeded(tui);
        }

        private void RecalculateLoopFraction(TrackUi tui)
        {
            if (!tui.LoopEnabled || tui.LoopDenominator <= 0)
            {
                tui.LoopBaseStartSamples = 0;
                tui.LoopBaseEndSamples = 0;
                tui.LoopFractionSamples = 0;
                // disable in audio immediately
                tui.Audio.UpdateLoopFraction(0, 0, 0, false, false);
                return;
            }
            long totalSamples = tui.Audio.Length;
            long regionStart = 0;
            long regionEnd = totalSamples; // default: full track when no selection
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
            // if already playing, apply immediately (adjust position if outside new fraction)
            bool adjust = true;
            tui.Audio.UpdateLoopFraction(regionStart, regionEnd, fraction, loopEnabled: true, adjustPosition: adjust);
            ForceJumpIntoLoopIfNeeded(tui);
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

            // If playing allow no new selection start that would reposition; if paused we allow selection but no reposition on click
            bool blockSelect = tui.Audio.Playing;
            if (blockSelect)
            {
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
            // Ignore selection changes while playing
            if (tui.Audio.Playing)
            {
                return;
            }

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
            // Only block reposition while actively playing (paused should allow reposition)
            bool blockReposition = tui.Audio.Playing;
            if (tui.Audio.Playing)
            {
                // reset selection flags if playing (still allow drag selection cancellation)
                tui.PendingSelect = false;
                tui.DragSelecting = false;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                if (!tui.DragSelecting && tui.PendingSelect)
                {
                    // Simple click without drag
                    long frame = this.MapPixelToFrameInView(tui, e.X);
                    int ch = Math.Max(1, tui.Audio.Channels);
                    // Clear selection on simple click
                    tui.Audio.SelectionStart = -1;
                    tui.Audio.SelectionEnd = -1;
                    // Set start position if allowed
                    if (!blockReposition)
                    {
                        tui.Audio.SetPosition(frame);
                        tui.Audio.StartingOffset = frame * ch; // in samples
                        long desiredOffset = frame - (long)CaretAnchorPx * tui.SamplesPerPixel;
                        desiredOffset = Math.Max(0, desiredOffset);
                        long maxOffset = this.GetMaxOffsetFrames(tui);
                        tui.OffsetFrames = Math.Min(maxOffset, desiredOffset);
                        this.UpdateOffsetScrollbar(tui);
                    }
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
            this.NAudience_Forms_WindowMainView_Tracks_UpdateScrollBar();
            this.LayoutTracks();
        }

        private Task ExportTrackAsync(TrackUi tui, string format, int bitsOrBitrate)
        {
            return this.AudioC.ExportAsync(tui.Audio.Id, format, bitsOrBitrate);
        }

        private static void ForceJumpIntoLoopIfNeeded(TrackUi tui)
        {
            if (!tui.LoopEnabled || tui.Audio == null) return;
            // Determine current fraction boundaries
            long total = tui.Audio.Length;
            long baseStart = (tui.Audio.SelectionStart >= 0 && tui.Audio.SelectionEnd > tui.Audio.SelectionStart) ? tui.Audio.SelectionStart : 0;
            long baseEnd = (tui.Audio.SelectionStart >= 0 && tui.Audio.SelectionEnd > tui.Audio.SelectionStart) ? Math.Min(total, tui.Audio.SelectionEnd) : total;
            if (baseEnd <= baseStart) return;
            long fractionLen = (baseEnd - baseStart) / Math.Max(1, tui.LoopDenominator);
            fractionLen = Math.Max(1, fractionLen);
            long loopFracEnd = Math.Min(baseStart + fractionLen, baseEnd);
            int ch = Math.Max(1, tui.Audio.Channels);
            long curSamples = tui.Audio.Position * ch;
            if (curSamples < baseStart || curSamples >= loopFracEnd)
            {
                // Jump acoustically & visually
                tui.Audio.JumpToSamples(baseStart);
                // Align view offset so caret shows loop start
                long loopStartFrame = baseStart / ch;
                long desiredOffsetFrame = loopStartFrame - (long) CaretAnchorPx * tui.SamplesPerPixel;
                desiredOffsetFrame = Math.Max(0, desiredOffsetFrame);
                long maxOffset = tui.Audio.Length / ch - (long) tui.PictureWave.Width * tui.SamplesPerPixel;
                maxOffset = Math.Max(0, maxOffset);
                tui.OffsetFrames = Math.Min(maxOffset, desiredOffsetFrame);
            }
        }

        private void AlignViewToCurrentPosition(TrackUi tui)
        {
            int ch = Math.Max(1, tui.Audio.Channels);
            long frame = tui.Audio.Position; // assume Position already frame-based
            long desiredOffset = frame - (long)CaretAnchorPx * tui.SamplesPerPixel;
            desiredOffset = Math.Max(0, desiredOffset);
            long maxOffset = this.GetMaxOffsetFrames(tui);
            tui.OffsetFrames = Math.Min(maxOffset, desiredOffset);
            this.UpdateOffsetScrollbar(tui);
        }
    }
}
