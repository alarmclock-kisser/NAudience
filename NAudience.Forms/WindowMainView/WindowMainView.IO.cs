using NAudience.Core;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NAudience.Forms
{
	public partial class WindowMainView
	{
		private async void button_import_Click(object sender, EventArgs e)
		{
			if (ModifierKeys.HasFlag(Keys.Alt))
			{
				await this.ImportRandomResourceAsync();
				return;
			}

			// Shift ohne Ctrl = Ordner laden
			if (ModifierKeys.HasFlag(Keys.Shift) && !ModifierKeys.HasFlag(Keys.Control))
			{
				await this.ImportDirectoryAsync();
			}
			else
			{
				await this.ImportFilesAsync();
			}
		}

		private async Task ImportRandomResourceAsync()
		{
			var resourceAudios = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"), "*.*", SearchOption.AllDirectories)
				.Select(AudioCollection.VerifyAudioFile)
				.Where(path => path != null)
				.ToList();
			if (resourceAudios.Count <= 0)
			{
				LogCollection.Log("No valid resource audio files found.");
				return;
			}
			var random = new Random();
			var randomFilePath = resourceAudios[random.Next(0, resourceAudios.Count)]!;
			var audioObj = await this.AudioC.LoadAsync(randomFilePath);
			if (audioObj == null)
			{
				LogCollection.Log($"Failed to load audio file: {randomFilePath}");
				return;
			}
			LogCollection.Log($"Loaded random resource audio sample: {audioObj.Name}");
		}

		private async Task ImportDirectoryAsync()
		{
			using var fbd = new FolderBrowserDialog()
			{
				Description = "Select Directory Containing Audio Files",
				UseDescriptionForTitle = true,
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
			};
			if (fbd.ShowDialog(this) == DialogResult.OK)
			{
				var dirPath = fbd.SelectedPath;
				var loadedAudioObjs = await this.AudioC.LoadDirectoryAsync(dirPath);
				int loadedCount = loadedAudioObjs.Count(a => a != null);
				LogCollection.Log($"{loadedCount} audio samples loaded from directory.");
			}
		}

		private async Task ImportFilesAsync()
		{
			string initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
			if (ModifierKeys.HasFlag(Keys.Shift) && ModifierKeys.HasFlag(Keys.Control))
			{
				initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
			}
			using var ofd = new OpenFileDialog()
			{
				Filter = "Audio Files|*.wav;*.mp3;*.flac",
				Title = "Select Audio File(s)",
				Multiselect = true,
				InitialDirectory = initialDir,
				RestoreDirectory = true
			};
			if (ofd.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}

			int loadedCount = 0;
			foreach (var filePath in ofd.FileNames)
			{
				var verifiedPath = AudioCollection.VerifyAudioFile(filePath);
				if (verifiedPath == null)
				{
					LogCollection.Log($"Unsupported audio format: {filePath}");
					continue;
				}
				var audioObj = await AudioObj.FromFileAsync(verifiedPath);
				if (audioObj == null)
				{
					LogCollection.Log($"Failed to load audio file: {filePath}");
					continue;
				}
				this.AudioC.Audios.Add(audioObj);
				loadedCount++;
			}
			LogCollection.Log($"{loadedCount} audio samples loaded.");
		}
	}
}
