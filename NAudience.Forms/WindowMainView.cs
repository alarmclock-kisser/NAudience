using NAudience.Core;
using NAudience.Forms;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.ComponentModel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace NAudience.Forms
{
	public partial class WindowMainView : Form
	{
		public readonly AudioCollection AudioC = new();

		public double FrameRate { get; set; } = 30.0;

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
		public const int MinTrackHeight = 100;
		public const int MaxTrackHeight = 280;

		private readonly ConcurrentDictionary<Guid, TrackUi> trackUis = new();
		private System.Windows.Forms.Timer? frameTimer;
		private bool frameBusy = false;

		public WindowMainView()
		{
			this.InitializeComponent();

			this.FrameRate = WindowsScreenHelper.GetScreenRefreshRate();

			 // UI Event-Hander für Einstellungen
			this.InitializeSettingsHandlers();
			// Template ausblenden & initialisieren
			this.InitializeTrackTemplate();
			// Binding beobachten
			this.AudioC.Audios.ListChanged += this.Audios_ListChanged;
			// Timer
			this.InitializeFrameTimer();
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
	}
}
