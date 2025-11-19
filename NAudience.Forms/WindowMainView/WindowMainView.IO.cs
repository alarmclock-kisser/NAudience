using NAudience.Core;
using System;
using System.Collections.Generic;
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

        private static readonly string[] ResourceExtensions = [".wav", ".mp3", ".flac"];

        private async Task<IReadOnlyList<string>> GetResourceAudioFilesAsync(bool forceRefresh = false)
        {
            bool shouldRefresh;
            lock (this.resourceCacheGate)
            {
                shouldRefresh = forceRefresh
                    || this.cachedResourceAudioPaths == null
                    || DateTime.UtcNow - this.resourceCacheTimestamp > ResourceCacheTtl;
                if (!shouldRefresh)
                {
                    return this.cachedResourceAudioPaths!.ToList();
                }
            }

            var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            if (!Directory.Exists(baseDir))
            {
                lock (this.resourceCacheGate)
                {
                    this.cachedResourceAudioPaths = [];
                    this.resourceCacheTimestamp = DateTime.UtcNow;
                    return this.cachedResourceAudioPaths.ToList();
                }
            }

            var files = await Task.Run(() =>
            {
                return Directory.EnumerateFiles(baseDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => ResourceExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }).ConfigureAwait(true);

            lock (this.resourceCacheGate)
            {
                this.cachedResourceAudioPaths = files;
                this.resourceCacheTimestamp = DateTime.UtcNow;
                return this.cachedResourceAudioPaths.ToList();
            }
        }

        private void RemoveResourceFromCache(string filePath)
        {
            lock (this.resourceCacheGate)
            {
                if (this.cachedResourceAudioPaths == null)
                {
                    return;
                }
                this.cachedResourceAudioPaths.RemoveAll(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));
            }
        }

        private async Task ImportRandomResourceAsync()
        {
            var resourceAudios = new List<string>(await this.GetResourceAudioFilesAsync());
            if (resourceAudios.Count <= 0)
            {
                LogCollection.Log("No valid resource audio files found.");
                return;
            }
            string? verifiedPath = null;
            for (int attempt = 0; attempt < resourceAudios.Count; attempt++)
            {
                var pick = resourceAudios[Random.Shared.Next(0, resourceAudios.Count)];
                var candidate = AudioCollection.VerifyAudioFile(pick);
                if (candidate != null)
                {
                    verifiedPath = candidate;
                    break;
                }
                this.RemoveResourceFromCache(pick);
                resourceAudios.Remove(pick);
                if (resourceAudios.Count == 0)
                {
                    break;
                }
            }
            if (verifiedPath == null)
            {
                LogCollection.Log("No valid resource audio files available after verification.");
                return;
            }
            var audioObj = await this.AudioC.LoadAsync(verifiedPath);
            if (audioObj == null)
            {
                LogCollection.Log($"Failed to load audio file: {verifiedPath}");
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
