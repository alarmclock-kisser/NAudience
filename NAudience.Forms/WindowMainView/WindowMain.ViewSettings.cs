using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NAudience.Forms
{
    public partial class WindowMainView
    {
        private void InitializeSettingsHandlers()
        {
            this.button_colorWave.Click += this.button_colorWave_Click;
            this.button_colorBack.Click += this.button_colorBack_Click;
            this.button_colorBack.MouseDown += this.button_colorBack_MouseDown;
            this.button_colorCaret.Click += this.button_colorCaret_Click;
            this.button_colorSelection.Click += this.button_colorSelection_Click;
            this.checkBox_hue.CheckedChanged += this.checkBox_hue_CheckedChanged;
            this.button_strobe.Click += this.button_strobe_Click;
            this.numericUpDown_hue.ValueChanged += this.numericUpDown_hue_ValueChanged;
            // apply fit height immediately when toggled
            this.checkBox_fitHeight.CheckedChanged += this.checkBox_fitHeight_CheckedChanged;
        }

        private void checkBox_fitHeight_CheckedChanged(object? sender, EventArgs e)
        {
            this.FitTracksToHeightIfEnabled();
        }

        private void button_colorWave_Click(object? sender, EventArgs e)
        {
            using ColorDialog colorDialog = new()
            {
                AllowFullOpen = true,
                AnyColor = true,
                FullOpen = true,
                Color = this.ColorWave
            };

            if (colorDialog.ShowDialog(this) == DialogResult.OK)
            {
                var chosen = colorDialog.Color;
                this.button_colorWave.BackColor = chosen;
                this.button_colorWave.ForeColor = chosen.GetBrightness() < 0.6f ? Color.White : Color.Black;
            }
        }

        private void button_colorBack_Click(object? sender, EventArgs e)
        {
            using ColorDialog colorDialog = new()
            {
                AllowFullOpen = true,
                AnyColor = true,
                FullOpen = true,
                Color = this.BackColor,
            };

            if (colorDialog.ShowDialog(this) == DialogResult.OK)
            {
                this.button_colorBack.BackColor = colorDialog.Color;
                this.button_colorBack.ForeColor = this.BackColor.GetBrightness() < 0.6f ? Color.White : Color.Black;
            }
        }

        private void button_colorBack_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                this.BackColor = GetNegativeColor(this.BackColor);
                this.button_colorBack.BackColor = GetShadedColor(this.BackColor, 0.95f);
                this.button_colorBack.ForeColor = this.BackColor.GetBrightness() < 0.6f ? Color.White : Color.Black;
            }
            else if (e.Button == MouseButtons.Left)
            {
                this.button_colorBack_Click(sender, EventArgs.Empty);
            }
        }

        private void button_colorCaret_Click(object? sender, EventArgs e)
        {
            using ColorDialog colorDialog = new()
            {
                AllowFullOpen = true,
                AnyColor = true,
                FullOpen = true,
                Color = this.ColorCaret,
            };
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                this.button_colorCaret.BackColor = colorDialog.Color;
                this.button_colorCaret.ForeColor = this.ColorCaret.GetBrightness() < 0.6f ? Color.White : Color.Black;
            }
        }

        private void button_colorSelection_Click(object? sender, EventArgs e)
        {
            using ColorDialog colorDialog = new()
            {
                AllowFullOpen = true,
                AnyColor = true,
                FullOpen = true,
                Color = this.ColorSelection
            };

            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                this.button_colorSelection.BackColor = colorDialog.Color;
                this.button_colorSelection.BackColor = GetFadedColor(colorDialog.Color, 0.25f);
                this.button_colorSelection.ForeColor = this.ColorSelection.GetBrightness() < 0.6f ? Color.White : Color.Black;
            }
        }

        private void checkBox_hue_CheckedChanged(object? sender, EventArgs e)
        {
            if (this.checkBox_hue.Checked)
            {
                this.numericUpDown_hue.Enabled = !this.StrobeEnabled;
                if (this.numericUpDown_hue.Value <= 0)
                {
                    this.numericUpDown_hue.Value = 1.75m;
                }
                this.StoredHueValue = (float) this.numericUpDown_hue.Value;

                if (this.StrobeEnabled)
                {
                    this.HueAdjustment = this.StrobeHueAdjustment;
                    this.numericUpDown_hue.Enabled = false;
                }
                else
                {
                    this.HueAdjustment = this.DefaultHueAdjustment;
                    this.numericUpDown_hue.Enabled = true;
                }
            }
            else
            {
                this.button_strobe.ForeColor = Color.Black;
                this.numericUpDown_hue.Enabled = false;
                this.StoredHueValue = 0.0f;
                this.HueAdjustment = 0.0f;
            }
            // Reflect hue in wave color button when toggled
            if (this.checkBox_hue.Checked)
            {
                this.button_colorWave.BackColor = this.HueColor;
                this.button_colorWave.ForeColor = this.HueColor.GetBrightness() < 0.6f ? Color.White : Color.Black;
            }
        }

        private void button_strobe_Click(object? sender, EventArgs e)
        {
            bool strobeOn = this.button_strobe.ForeColor != Color.Red;

            if (strobeOn)
            {
                this.button_strobe.ForeColor = Color.Red;
                this.button_strobe.Text = "☠️";
                this.checkBox_hue.Checked = true;
                this.HueAdjustment = this.StrobeHueAdjustment;
                this.numericUpDown_hue.Enabled = false;
            }
            else
            {
                this.button_strobe.ForeColor = Color.Black;
                this.button_strobe.Text = "⚡";
                this.HueAdjustment = this.DefaultHueAdjustment;
                this.numericUpDown_hue.Enabled = true;
            }
            // Reflect current hue
            this.button_colorWave.BackColor = this.HueColor;
            this.button_colorWave.ForeColor = this.HueColor.GetBrightness() < 0.6f ? Color.White : Color.Black;
        }

        private void numericUpDown_hue_ValueChanged(object? sender, EventArgs e)
        {
            this.StoredHueValue = (float) this.numericUpDown_hue.Value;
            if (!this.StrobeEnabled && this.HueEnabled)
            {
                this.HueAdjustment = this.DefaultHueAdjustment;
            }
            // Update preview color when changed
            if (this.checkBox_hue.Checked)
            {
                this.button_colorWave.BackColor = this.HueColor;
                this.button_colorWave.ForeColor = this.HueColor.GetBrightness() < 0.6f ? Color.White : Color.Black;
            }
        }

        private Color GetNextHue(float? increment = null, bool updateHueColor = true)
        {
            increment ??= this.HueAdjustment;

            float currentHue = this.HueColor.GetHue();
            float newHue = (currentHue + increment.Value) % 360f;

            if (updateHueColor)
            {
                this.HueColor = ColorFromHSV(newHue, 1.0f, 1.0f);
            }

            return ColorFromHSV(newHue, 1.0f, 1.0f);
        }

        public static Color GetNegativeColor(Color color)
        {
            return Color.FromArgb(color.A, 255 - color.R, 255 - color.G, 255 - color.B);
        }

        public static Color GetShadedColor(Color color, float factor = 0.67f)
        {
            factor = Math.Clamp(factor, 0.0f, 1.0f);
            return Color.FromArgb(
                color.A,
                (int) (color.R * factor),
                (int) (color.G * factor),
                (int) (color.B * factor)
            );
        }

        public static Color GetFadedColor(Color color, float alphaFactor = 0.5f)
        {
            alphaFactor = Math.Clamp(alphaFactor, 0.0f, 1.0f);
            return Color.FromArgb(
                (int) (color.A * alphaFactor),
                color.R,
                color.G,
                color.B
            );
        }

        public static Color ColorFromHSV(float hue, float saturation, float value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            float f = hue / 60 - (float) Math.Floor(hue / 60);
            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));
            return hi switch
            {
                0 => Color.FromArgb(255, v, t, p),
                1 => Color.FromArgb(255, q, v, p),
                2 => Color.FromArgb(255, p, v, t),
                3 => Color.FromArgb(255, p, q, v),
                4 => Color.FromArgb(255, t, p, v),
                _ => Color.FromArgb(255, v, p, q),
            };
        }
    }
}
