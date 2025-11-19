using NAudience.Core;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NAudience.Forms
{
    public partial class WindowMainView
    {
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
            long visibleFrames = (long) tui.PictureWave.Width * tui.SamplesPerPixel;
            return Math.Max(0, totalFrames - visibleFrames);
        }

        private long MapPixelToFrame(AudioObj audio, int pixelX, int width)
        {
            pixelX = Math.Clamp(pixelX, 0, width);
            long totalFrames = audio.Length / Math.Max(1, audio.Channels);
            if (totalFrames <= 0) return 0;
            return (long) (totalFrames * (pixelX / (double) width));
        }

        private long MapPixelToFrameInView(TrackUi tui, int pixelX)
        {
            pixelX = Math.Clamp(pixelX, 0, tui.PictureWave.Width);
            long frame = tui.OffsetFrames + (long) pixelX * tui.SamplesPerPixel;
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
                    long desiredOffset = posFrames - (long) CaretAnchorPx * tui.SamplesPerPixel;
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
                    double px = (posFrames - offsetFrames) / (double) Math.Max(1, tui.SamplesPerPixel);
                    caretPosNorm = (float) Math.Clamp(px / Math.Max(1, tui.PictureWave.Width - 1), 0.0, 1.0);
                }
                else
                {
                    caretPosNorm = Math.Clamp(CaretAnchorPx / (float) Math.Max(1, tui.PictureWave.Width - 1), 0f, 1f);
                }

                // dynamic hue color if enabled
                Color waveColor = this.checkBox_hue.Checked ? this.HueColor : this.ColorWave;
                var bmp = await tui.Audio.DrawWaveformAsync(tui.PictureWave.Width, tui.PictureWave.Height,
                     samplesPerPixel: tui.SamplesPerPixel, waveColor: waveColor, backColor: this.ColorBack, caretColor: this.ColorCaret,
                     caretPosition: caretPosNorm, offset: offsetFrames);

                // Selection overlay inside visible range using frames
                if (tui.Audio.SelectionStart >= 0 && tui.Audio.SelectionEnd > tui.Audio.SelectionStart)
                {
                    using var g = Graphics.FromImage(bmp);
                    long visStartFrames = offsetFrames;
                    long visEndFrames = visStartFrames + (long) tui.PictureWave.Width * tui.SamplesPerPixel;
                    long selStartFrames = tui.Audio.SelectionStart / channels;
                    long selEndFrames = tui.Audio.SelectionEnd / channels;
                    long drawStart = Math.Max(selStartFrames, visStartFrames);
                    long drawEnd = Math.Min(selEndFrames, visEndFrames);
                    if (drawEnd > drawStart)
                    {
                        float pxStart = (drawStart - visStartFrames) / (float) tui.SamplesPerPixel;
                        float pxEnd = (drawEnd - visStartFrames) / (float) tui.SamplesPerPixel;
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

        private void UpdateOffsetScrollbar(TrackUi tui)
        {
            long maxOffset = this.GetMaxOffsetFrames(tui);
            long visibleFrames = (long) tui.PictureWave.Width * tui.SamplesPerPixel;
            int sbMax = (int) Math.Min(int.MaxValue - 1, maxOffset);
            int large = (int) Math.Min(int.MaxValue / 8, Math.Max(1, visibleFrames));
            int small = Math.Max(1, large / 10);
            var sb = tui.ScrollOffset;
            sb.Minimum = 0;
            sb.LargeChange = large;
            sb.SmallChange = small;
            sb.Maximum = sbMax + sb.LargeChange; // WinForms quirk
            sb.Enabled = sbMax > 0;
            sb.Value = (int) Math.Min(sbMax, tui.OffsetFrames);
        }
    }
}
