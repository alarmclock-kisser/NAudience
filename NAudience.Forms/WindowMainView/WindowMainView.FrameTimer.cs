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
        private void InitializeFrameTimer()
        {
            this.frameTimer = new System.Windows.Forms.Timer();
            int interval = (int) Math.Max(1, 1000.0 / this.FrameRate);
            this.frameTimer.Interval = interval;
            this.frameTimer.Tick += async (_, __) => await this.FrameTickAsync();
            this.frameTimer.Start();
        }

        private async Task FrameTickAsync()
        {
            if (this.frameBusy)
            {
                return;
            }

            this.frameBusy = true;
            try
            {
                // Advance hue if enabled
                if (this.checkBox_hue.Checked)
                {
                    float inc = Math.Abs(this.HueAdjustment) > 0.0001f ? this.HueAdjustment : this.StoredHueValue;
                    this.GetNextHue(inc, updateHueColor: true);
                }

                var tasks = this.trackUis.Values.Select(async tui =>
                 {
                     // Zeit aktualisieren
                     TimeSpan current;
                     if (tui.Audio.Playing)
                     {
                         current = TimeSpan.FromSeconds(tui.Audio.Position / (double) Math.Max(1, tui.Audio.SampleRate));
                         if (tui.TextTime.ForeColor != Color.Black) tui.TextTime.ForeColor = Color.Black;
                     }
                     else
                     {
                         // Not playing: show mouse hover time (blue) or caret-scrolled time (black)
                         long frameForTime;
                         if (tui.MouseOver)
                         {
                             frameForTime = tui.OffsetFrames + (long) Math.Clamp(tui.MouseX, 0, Math.Max(1, tui.PictureWave.Width)) * tui.SamplesPerPixel;
                             current = TimeSpan.FromSeconds(frameForTime / (double) Math.Max(1, tui.Audio.SampleRate));
                             if (tui.TextTime.ForeColor != Color.RoyalBlue) tui.TextTime.ForeColor = Color.RoyalBlue;
                         }
                         else
                         {
                             // caret anchor position in current view
                             long maxOff = Math.Max(0, this.GetMaxOffsetFrames(tui));
                             long caretFrame = tui.OffsetFrames >= maxOff
                                 ? Math.Min(this.GetTotalFrames(tui.Audio) - 1, tui.OffsetFrames + (long) tui.PictureWave.Width * tui.SamplesPerPixel - 1)
                                 : tui.OffsetFrames + (long) CaretAnchorPx * tui.SamplesPerPixel;
                             current = TimeSpan.FromSeconds(caretFrame / (double) Math.Max(1, tui.Audio.SampleRate));
                             if (tui.TextTime.ForeColor != Color.Black) tui.TextTime.ForeColor = Color.Black;
                         }
                     }
                     // Format
                     tui.TextTime.Text = current.ToString("c");
                     await this.RefreshWaveformAsync(tui);
                 });
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                LogCollection.Log(ex);
            }
            finally { this.frameBusy = false; }
        }
    }
}
