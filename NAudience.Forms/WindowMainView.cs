using NAudience.Core;
using NAudience.Forms;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace NAudience.Forms
{
    public partial class WindowMainView : Form
    {
        public readonly AudioCollection AudioC = new();

        public double FrameRate { get; set; }

        public Color ColorWave => this.button_colorWave.BackColor;
        public Color ColorBack => this.button_colorBack.BackColor;
        public Color ColorCaret => this.button_colorCaret.BackColor;
        public Color ColorSelection => this.button_colorSelection.BackColor;

        public bool StrobeEnabled { get; set; } = false;
        public bool HueEnabled { get; set; } = false;
        public Color HueColor { get; set; } = Color.FromArgb(255, 255, 0, 0);
        public float StoredHueValue { get; set; } = 1.75f;
        public float HueAdjustment { get; set; } = 0.0f;
        public float StrobeHueAdjustment { get; set; } = 155.77f;
        public float DefaultHueAdjustment { get; set; } = 0.0f;

        public int TrackHeight { get; set; } = 140;
        public const int MinTrackHeight = 110;
        public const int MaxTrackHeight = 320;

        private readonly ConcurrentDictionary<Guid, TrackUi> trackUis = new();
        private System.Windows.Forms.Timer? frameTimer;
        private bool frameBusy = false;

        private int FixedClientWidth => this.Width;
        private readonly object resourceCacheGate = new();
        private List<string>? cachedResourceAudioPaths;
        private DateTime resourceCacheTimestamp = DateTime.MinValue;
        private static readonly TimeSpan ResourceCacheTtl = TimeSpan.FromMinutes(5);

        public WindowMainView()
        {
            this.InitializeComponent();

            this.FrameRate = WindowsScreenHelper.GetScreenRefreshRate();

            // Lock width to a fixed client width (height resizable only)
            this.ClientSize = new Size(FixedClientWidth, this.ClientSize.Height);
            int border = this.Width - this.ClientSize.Width; // non-client width (borders)
            this.MinimumSize = new Size(FixedClientWidth + border, 240);
            this.MaximumSize = new Size(FixedClientWidth + border, int.MaxValue);
            this.Resize += (_, __) => this.HandleResizeLayout();

            // UI Event-Hander f�r Einstellungen
            this.InitializeSettingsHandlers();
            // Template ausblenden & initialisieren
            this.InitializeTrackTemplate();
            // Binding beobachten
            this.AudioC.Audios.ListChanged += this.Audios_ListChanged;
            // Timer
            this.InitializeFrameTimer();
        }

        private void HandleResizeLayout()
        {
            // keep controls panel static at top
            this.panel_controls.Location = new Point(12, 12);
            // stretch waveforms panel below controls to bottom, keep right margin
            int left = this.panel_waveforms.Location.X;
            int top = this.panel_controls.Bottom + 6;
            int width = this.ClientSize.Width - left - 12;
            int height = Math.Max(1, this.ClientSize.Height - top - 12);
            this.panel_waveforms.Location = new Point(left, top);
            this.panel_waveforms.Size = new Size(Math.Max(100, width), Math.Max(1, height));
            // adjust track scrollbar height and right alignment
            this.vScrollBar_tracks.Location = new Point(this.panel_waveforms.Width - this.vScrollBar_tracks.Width - 3, 4);
            this.vScrollBar_tracks.Height = Math.Max(8, this.panel_waveforms.Height - 8);
            // re-layout tracks and scrollbars based on new sizes
            this.NAudience_Forms_WindowMainView_Tracks_UpdateScrollBar();
            this.LayoutTracks();
            // Only grow to fit when enabled, but always shrink to avoid clipping
            if (this.checkBox_fitHeight.Checked)
            {
                this.FitTracksToHeightIfEnabled();
            }
            else
            {
                this.EnsureNoClippingTrackHeights();
            }
        }

        private async void button_export_Click(object sender, EventArgs e)
        {
            string exportDirectory = Path.GetFullPath(this.AudioC.ExportPath);
            string batchExportPath = exportDirectory;
            // TODO: Export-Dialog / Batch-Export implementieren
        }

        private void Audios_ListChanged(object? sender, ListChangedEventArgs e)
        {
            // Rebuild bei Reset, ItemAdded/Deleted
            if (e.ListChangedType is ListChangedType.ItemAdded)
            {
                var audio = this.AudioC.Audios[e.NewIndex];
                _ = this.BuildTrackAsync(audio); // fire & forget
            }
            else if (e.ListChangedType is ListChangedType.ItemDeleted || e.ListChangedType is ListChangedType.Reset)
            {
                this.RebuildAllTracks();
            }
        }

        private void button_browse_Click(object sender, EventArgs e)
        {
            // Open an explorer window at the export path
            string exportDirectory = Path.GetFullPath(this.AudioC.ExportPath);
            string batchExportPath = exportDirectory;
            try
            {
                if (!Directory.Exists(batchExportPath))
                {
                    Directory.CreateDirectory(batchExportPath);
                }
                Process.Start("explorer.exe", batchExportPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open export directory:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

	}
}
