namespace NAudience.Forms
{
    partial class WindowMainView
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panel_waveforms = new Panel();
            this.vScrollBar_tracks = new VScrollBar();
            this.panel_track = new Panel();
            this.label_volume = new Label();
            this.hScrollBar_offset = new HScrollBar();
            this.vScrollBar_volume = new VScrollBar();
            this.pictureBox_waveform = new PictureBox();
            this.label_name = new Label();
            this.textBox_time = new TextBox();
            this.button_loop = new Button();
            this.button_playback = new Button();
            this.button_pause = new Button();
            this.panel_controls = new Panel();
            this.button_browse = new Button();
            this.checkBox_fitHeight = new CheckBox();
            this.button_strobe = new Button();
            this.numericUpDown_hue = new NumericUpDown();
            this.checkBox_hue = new CheckBox();
            this.button_export = new Button();
            this.label_info_colors = new Label();
            this.button_colorSelection = new Button();
            this.button_colorCaret = new Button();
            this.button_colorBack = new Button();
            this.button_colorWave = new Button();
            this.button_import = new Button();
            this.panel_waveforms.SuspendLayout();
            this.panel_track.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize) this.pictureBox_waveform).BeginInit();
            this.panel_controls.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize) this.numericUpDown_hue).BeginInit();
            this.SuspendLayout();
            // 
            // panel_waveforms
            // 
            this.panel_waveforms.BackColor = SystemColors.Control;
            this.panel_waveforms.Controls.Add(this.vScrollBar_tracks);
            this.panel_waveforms.Controls.Add(this.panel_track);
            this.panel_waveforms.Location = new Point(12, 88);
            this.panel_waveforms.Name = "panel_waveforms";
            this.panel_waveforms.Size = new Size(1040, 581);
            this.panel_waveforms.TabIndex = 0;
            // 
            // vScrollBar_tracks
            // 
            this.vScrollBar_tracks.Location = new Point(1020, 4);
            this.vScrollBar_tracks.Name = "vScrollBar_tracks";
            this.vScrollBar_tracks.Size = new Size(17, 577);
            this.vScrollBar_tracks.TabIndex = 1;
            // 
            // panel_track
            // 
            this.panel_track.BackColor = SystemColors.ActiveBorder;
            this.panel_track.Controls.Add(this.label_volume);
            this.panel_track.Controls.Add(this.hScrollBar_offset);
            this.panel_track.Controls.Add(this.vScrollBar_volume);
            this.panel_track.Controls.Add(this.pictureBox_waveform);
            this.panel_track.Controls.Add(this.label_name);
            this.panel_track.Controls.Add(this.textBox_time);
            this.panel_track.Controls.Add(this.button_loop);
            this.panel_track.Controls.Add(this.button_playback);
            this.panel_track.Controls.Add(this.button_pause);
            this.panel_track.Location = new Point(3, 3);
            this.panel_track.Name = "panel_track";
            this.panel_track.Size = new Size(1014, 140);
            this.panel_track.TabIndex = 0;
            // 
            // label_volume
            // 
            this.label_volume.AutoSize = true;
            this.label_volume.Font = new Font("Bahnschrift SemiCondensed", 9F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.label_volume.Location = new Point(58, 120);
            this.label_volume.Name = "label_volume";
            this.label_volume.Size = new Size(35, 14);
            this.label_volume.TabIndex = 2;
            this.label_volume.Text = "80.0%";
            // 
            // hScrollBar_offset
            // 
            this.hScrollBar_offset.Location = new Point(99, 120);
            this.hScrollBar_offset.Name = "hScrollBar_offset";
            this.hScrollBar_offset.Size = new Size(912, 15);
            this.hScrollBar_offset.TabIndex = 1;
            // 
            // vScrollBar_volume
            // 
            this.vScrollBar_volume.Location = new Point(81, 20);
            this.vScrollBar_volume.Name = "vScrollBar_volume";
            this.vScrollBar_volume.Size = new Size(15, 97);
            this.vScrollBar_volume.TabIndex = 1;
            // 
            // pictureBox_waveform
            // 
            this.pictureBox_waveform.BackColor = Color.White;
            this.pictureBox_waveform.Location = new Point(99, 19);
            this.pictureBox_waveform.Name = "pictureBox_waveform";
            this.pictureBox_waveform.Size = new Size(912, 98);
            this.pictureBox_waveform.TabIndex = 1;
            this.pictureBox_waveform.TabStop = false;
            // 
            // label_name
            // 
            this.label_name.AutoSize = true;
            this.label_name.Font = new Font("Bahnschrift SemiLight", 9F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.label_name.Location = new Point(3, 1);
            this.label_name.Margin = new Padding(1, 1, 3, 1);
            this.label_name.Name = "label_name";
            this.label_name.Size = new Size(136, 14);
            this.label_name.TabIndex = 1;
            this.label_name.Text = "No track name available.";
            // 
            // textBox_time
            // 
            this.textBox_time.Location = new Point(3, 94);
            this.textBox_time.MaxLength = 32;
            this.textBox_time.Name = "textBox_time";
            this.textBox_time.PlaceholderText = "0:00:00.000";
            this.textBox_time.ReadOnly = true;
            this.textBox_time.Size = new Size(75, 23);
            this.textBox_time.TabIndex = 1;
            this.textBox_time.TabStop = false;
            // 
            // button_loop
            // 
            this.button_loop.Font = new Font("Segoe UI Symbol", 9F, FontStyle.Bold, GraphicsUnit.Point,  0);
            this.button_loop.Location = new Point(55, 20);
            this.button_loop.Margin = new Padding(1, 3, 3, 3);
            this.button_loop.Name = "button_loop";
            this.button_loop.Size = new Size(23, 23);
            this.button_loop.TabIndex = 3;
            this.button_loop.Text = "↺";
            this.button_loop.UseVisualStyleBackColor = true;
            // 
            // button_playback
            // 
            this.button_playback.Location = new Point(3, 20);
            this.button_playback.Margin = new Padding(3, 3, 3, 1);
            this.button_playback.Name = "button_playback";
            this.button_playback.Size = new Size(23, 23);
            this.button_playback.TabIndex = 1;
            this.button_playback.Tag = "■";
            this.button_playback.Text = "▶";
            this.button_playback.UseVisualStyleBackColor = true;
            // 
            // button_pause
            // 
            this.button_pause.Font = new Font("Bahnschrift", 9F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.button_pause.Location = new Point(30, 20);
            this.button_pause.Margin = new Padding(1, 3, 1, 3);
            this.button_pause.Name = "button_pause";
            this.button_pause.Size = new Size(23, 23);
            this.button_pause.TabIndex = 2;
            this.button_pause.Text = "||";
            this.button_pause.UseVisualStyleBackColor = true;
            // 
            // panel_controls
            // 
            this.panel_controls.BackColor = SystemColors.Control;
            this.panel_controls.Controls.Add(this.button_browse);
            this.panel_controls.Controls.Add(this.checkBox_fitHeight);
            this.panel_controls.Controls.Add(this.button_strobe);
            this.panel_controls.Controls.Add(this.numericUpDown_hue);
            this.panel_controls.Controls.Add(this.checkBox_hue);
            this.panel_controls.Controls.Add(this.button_export);
            this.panel_controls.Controls.Add(this.label_info_colors);
            this.panel_controls.Controls.Add(this.button_colorSelection);
            this.panel_controls.Controls.Add(this.button_colorCaret);
            this.panel_controls.Controls.Add(this.button_colorBack);
            this.panel_controls.Controls.Add(this.button_colorWave);
            this.panel_controls.Controls.Add(this.button_import);
            this.panel_controls.Location = new Point(12, 12);
            this.panel_controls.Margin = new Padding(3, 3, 5, 3);
            this.panel_controls.Name = "panel_controls";
            this.panel_controls.Size = new Size(1040, 70);
            this.panel_controls.TabIndex = 1;
            // 
            // button_browse
            // 
            this.button_browse.Font = new Font("Bahnschrift", 9F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.button_browse.Location = new Point(79, 44);
            this.button_browse.Name = "button_browse";
            this.button_browse.Size = new Size(32, 23);
            this.button_browse.TabIndex = 48;
            this.button_browse.Text = "[...]";
            this.button_browse.UseVisualStyleBackColor = true;
            this.button_browse.Click += this.button_browse_Click;
            // 
            // checkBox_fitHeight
            // 
            this.checkBox_fitHeight.AutoSize = true;
            this.checkBox_fitHeight.Location = new Point(762, 43);
            this.checkBox_fitHeight.Name = "checkBox_fitHeight";
            this.checkBox_fitHeight.Size = new Size(78, 19);
            this.checkBox_fitHeight.TabIndex = 47;
            this.checkBox_fitHeight.Text = "Fit Height";
            this.checkBox_fitHeight.UseVisualStyleBackColor = true;
            // 
            // button_strobe
            // 
            this.button_strobe.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.button_strobe.ForeColor = Color.Black;
            this.button_strobe.Location = new Point(1014, 42);
            this.button_strobe.Name = "button_strobe";
            this.button_strobe.Size = new Size(23, 23);
            this.button_strobe.TabIndex = 44;
            this.button_strobe.Text = "🕱";
            this.button_strobe.UseVisualStyleBackColor = true;
            // 
            // numericUpDown_hue
            // 
            this.numericUpDown_hue.DecimalPlaces = 3;
            this.numericUpDown_hue.Increment = new decimal(new int[] { 125, 0, 0, 196608 });
            this.numericUpDown_hue.Location = new Point(933, 42);
            this.numericUpDown_hue.Maximum = new decimal(new int[] { 720, 0, 0, 0 });
            this.numericUpDown_hue.Name = "numericUpDown_hue";
            this.numericUpDown_hue.Size = new Size(75, 23);
            this.numericUpDown_hue.TabIndex = 45;
            this.numericUpDown_hue.Value = new decimal(new int[] { 175, 0, 0, 131072 });
            // 
            // checkBox_hue
            // 
            this.checkBox_hue.AutoSize = true;
            this.checkBox_hue.Location = new Point(879, 44);
            this.checkBox_hue.Name = "checkBox_hue";
            this.checkBox_hue.Size = new Size(48, 19);
            this.checkBox_hue.TabIndex = 46;
            this.checkBox_hue.Text = "Hue";
            this.checkBox_hue.UseVisualStyleBackColor = true;
            // 
            // button_export
            // 
            this.button_export.BackColor = Color.FromArgb(  192,   255,   255);
            this.button_export.Location = new Point(3, 44);
            this.button_export.Name = "button_export";
            this.button_export.Size = new Size(70, 23);
            this.button_export.TabIndex = 43;
            this.button_export.Text = "Export";
            this.button_export.UseVisualStyleBackColor = false;
            this.button_export.Click += this.button_export_Click;
            // 
            // label_info_colors
            // 
            this.label_info_colors.AutoSize = true;
            this.label_info_colors.Location = new Point(831, 7);
            this.label_info_colors.Name = "label_info_colors";
            this.label_info_colors.Size = new Size(44, 15);
            this.label_info_colors.TabIndex = 42;
            this.label_info_colors.Text = "Colors:";
            // 
            // button_colorSelection
            // 
            this.button_colorSelection.BackColor = SystemColors.AppWorkspace;
            this.button_colorSelection.Font = new Font("Bahnschrift SemiLight SemiConde", 8.25F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.button_colorSelection.Location = new Point(999, 3);
            this.button_colorSelection.Margin = new Padding(1, 3, 1, 1);
            this.button_colorSelection.Name = "button_colorSelection";
            this.button_colorSelection.Size = new Size(38, 23);
            this.button_colorSelection.TabIndex = 41;
            this.button_colorSelection.Text = "Area";
            this.button_colorSelection.UseVisualStyleBackColor = false;
            // 
            // button_colorCaret
            // 
            this.button_colorCaret.BackColor = Color.IndianRed;
            this.button_colorCaret.Font = new Font("Bahnschrift SemiLight SemiConde", 8.25F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.button_colorCaret.ForeColor = Color.Black;
            this.button_colorCaret.Location = new Point(879, 3);
            this.button_colorCaret.Margin = new Padding(1, 3, 1, 1);
            this.button_colorCaret.Name = "button_colorCaret";
            this.button_colorCaret.Size = new Size(38, 23);
            this.button_colorCaret.TabIndex = 40;
            this.button_colorCaret.Text = "Caret";
            this.button_colorCaret.UseVisualStyleBackColor = false;
            // 
            // button_colorBack
            // 
            this.button_colorBack.BackColor = Color.White;
            this.button_colorBack.Font = new Font("Bahnschrift SemiLight SemiConde", 8.25F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.button_colorBack.Location = new Point(959, 3);
            this.button_colorBack.Margin = new Padding(1, 3, 1, 1);
            this.button_colorBack.Name = "button_colorBack";
            this.button_colorBack.Size = new Size(38, 23);
            this.button_colorBack.TabIndex = 39;
            this.button_colorBack.Text = "Back";
            this.button_colorBack.UseVisualStyleBackColor = false;
            // 
            // button_colorWave
            // 
            this.button_colorWave.BackColor = SystemColors.ActiveCaption;
            this.button_colorWave.Font = new Font("Bahnschrift SemiLight SemiConde", 8.25F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.button_colorWave.Location = new Point(919, 3);
            this.button_colorWave.Margin = new Padding(1, 3, 1, 1);
            this.button_colorWave.Name = "button_colorWave";
            this.button_colorWave.Size = new Size(38, 23);
            this.button_colorWave.TabIndex = 38;
            this.button_colorWave.Text = "Wave";
            this.button_colorWave.UseVisualStyleBackColor = false;
            // 
            // button_import
            // 
            this.button_import.BackColor = Color.FromArgb(  255,   255,   192);
            this.button_import.Location = new Point(3, 3);
            this.button_import.Name = "button_import";
            this.button_import.Size = new Size(70, 23);
            this.button_import.TabIndex = 1;
            this.button_import.Text = "Import";
            this.button_import.UseVisualStyleBackColor = false;
            this.button_import.Click += this.button_import_Click;
            // 
            // WindowMainView
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = SystemColors.ControlLight;
            this.ClientSize = new Size(1064, 681);
            this.Controls.Add(this.panel_controls);
            this.Controls.Add(this.panel_waveforms);
            this.Name = "WindowMainView";
            this.Text = "NAudience (Main View)";
            this.panel_waveforms.ResumeLayout(false);
            this.panel_track.ResumeLayout(false);
            this.panel_track.PerformLayout();
            ((System.ComponentModel.ISupportInitialize) this.pictureBox_waveform).EndInit();
            this.panel_controls.ResumeLayout(false);
            this.panel_controls.PerformLayout();
            ((System.ComponentModel.ISupportInitialize) this.numericUpDown_hue).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private Panel panel_waveforms;
		private Panel panel_track;
		private Panel panel_controls;
		private Button button_loop;
		private Button button_pause;
		private Button button_playback;
		private VScrollBar vScrollBar_volume;
		private PictureBox pictureBox_waveform;
		private Label label_name;
		private TextBox textBox_time;
		private HScrollBar hScrollBar_offset;
		private VScrollBar vScrollBar_tracks;
		private Button button_export;
		private Label label_info_colors;
		private Button button_colorSelection;
		private Button button_colorCaret;
		private Button button_colorBack;
		private Button button_colorWave;
		private Button button_import;
		private Button button_strobe;
		private NumericUpDown numericUpDown_hue;
		private CheckBox checkBox_hue;
        private Label label_volume;
        private CheckBox checkBox_fitHeight;
        private Button button_browse;
    }
}
