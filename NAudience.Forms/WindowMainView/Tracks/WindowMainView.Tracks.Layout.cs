using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NAudience.Forms
{
    public partial class WindowMainView
    {
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
                tui.Panel.Size = new Size(this.panel_track.Width, this.TrackHeight);
                this.UpdateTrackUiHeights(tui);
                index++;
            }
        }

        private void NAudience_Forms_WindowMainView_Tracks_UpdateScrollBar()
        {
            int totalHeight = this.AudioC.Count * this.TrackHeight;
            this.vScrollBar_tracks.Minimum = 0;
            this.vScrollBar_tracks.Maximum = Math.Max(0, totalHeight);
            this.vScrollBar_tracks.LargeChange = this.panel_waveforms.Height;
            this.vScrollBar_tracks.SmallChange = this.TrackHeight / 2;
        }

        private void UpdateTrackUiHeights(TrackUi tui)
        {
            // Update per-control vertical layout based on current TrackHeight using template offsets
            var pb = tui.PictureWave;
            pb.Location = this.pictureBox_waveform.Location;
            pb.Size = new Size(this.pictureBox_waveform.Width, Math.Max(1, this.TrackHeight - 42));
            var sbOff = tui.ScrollOffset;
            sbOff.Location = new Point(this.hScrollBar_offset.Location.X, Math.Max(0, this.TrackHeight - 20));
            sbOff.Size = this.hScrollBar_offset.Size;
            // Volume scrollbar should follow the waveform box height
            var sbVol = tui.ScrollVolume;
            sbVol.Location = new Point(this.vScrollBar_volume.Location.X, pb.Location.Y + 1);
            int volHeight = Math.Max(10, pb.Height - 2);
            sbVol.Size = new Size(this.vScrollBar_volume.Width, volHeight);
            // volume label at bottom line like offset
            tui.LabelVolume.Location = new Point(this.label_volume.Location.X, Math.Max(0, this.TrackHeight - 20));
            // time textbox anchored to bottom baseline next to volume scrollbar
            var tb = tui.TextTime;
            int tbX = this.textBox_time.Location.X;
            int tbY = Math.Max(0, sbOff.Location.Y - tb.Height);
            tb.Location = new Point(tbX, tbY);
        }

        private void FitTracksToHeightIfEnabled()
        {
            if (!this.checkBox_fitHeight.Checked)
            {
                return;
            }
            int count = this.AudioC.Count;
            if (count <= 0)
            {
                return;
            }
            int available = Math.Max(1, this.panel_waveforms.ClientSize.Height);
            int desired = available / count;
            int clamped = Math.Clamp(desired, MinTrackHeight, MaxTrackHeight);
            if (this.TrackHeight != clamped)
            {
                this.TrackHeight = clamped;
                this.LayoutTracks();
                this.NAudience_Forms_WindowMainView_Tracks_UpdateScrollBar();
                // refresh each waveform to reflect new height
                foreach (var audio in this.AudioC.Audios)
                {
                    if (this.trackUis.TryGetValue(audio.Id, out var tui))
                    {
                        this.UpdateTrackUiHeights(tui);
                        this.UpdateOffsetScrollbar(tui);
                        _ = this.RefreshWaveformAsync(tui);
                    }
                }
            }
        }

        private void EnsureNoClippingTrackHeights()
        {
            int count = this.AudioC.Count;
            if (count <= 0)
            {
                return;
            }
            int available = Math.Max(1, this.panel_waveforms.ClientSize.Height);
            int desired = available / count;
            // only shrink if current height is larger than desired; don't auto-grow here
            if (this.TrackHeight > desired)
            {
                int clamped = Math.Clamp(desired, MinTrackHeight, MaxTrackHeight);
                if (this.TrackHeight != clamped)
                {
                    this.TrackHeight = clamped;
                    this.LayoutTracks();
                    this.NAudience_Forms_WindowMainView_Tracks_UpdateScrollBar();
                    foreach (var audio in this.AudioC.Audios)
                    {
                        if (this.trackUis.TryGetValue(audio.Id, out var tui))
                        {
                            this.UpdateTrackUiHeights(tui);
                            this.UpdateOffsetScrollbar(tui);
                            _ = this.RefreshWaveformAsync(tui);
                        }
                    }
                }
            }
        }
    }
}
