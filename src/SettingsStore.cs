using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace CodexWatchdog
{
    public static class SettingsStore
    {
        private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexWatchdog");
        private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
        public static WatchSettings Load()
        {
            return Load(FilePath);
        }
        public static WatchSettings Load(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath, Encoding.UTF8);
                    var serializer = new JavaScriptSerializer();
                    WatchSettings settings = serializer.Deserialize<WatchSettings>(content);
                    if (settings != null)
                    {
                        var stored = serializer.DeserializeObject(content) as Dictionary<string, object>;
                        if (stored != null && !stored.ContainsKey("MonitorAllSources"))
                        {
                            settings.MonitorAllSources = String.IsNullOrWhiteSpace(settings.SelectedSourceId);
                            settings.SelectedSourceIds = settings.MonitorAllSources ? new List<string>() : new List<string> { settings.SelectedSourceId };
                        }
                        NormalizeScope(settings);
                        AlertSoundRouting.EnsureProfiles(settings);
                        settings.StallMinutes = Math.Max(1, Math.Min(180, settings.StallMinutes));
                        settings.SnoozeMinutes = Math.Max(1, Math.Min(60, settings.SnoozeMinutes));
                        settings.VolumePercent = Math.Max(5, Math.Min(100, settings.VolumePercent));
                        return settings;
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (NotSupportedException) { }
            catch (System.Security.SecurityException) { }
            WatchSettings fallback=new WatchSettings();
            AlertSoundRouting.EnsureProfiles(fallback);
            return fallback;
        }
        public static bool Save(WatchSettings settings)
        {
            return Save(settings,FilePath);
        }
        public static bool Save(WatchSettings settings,string filePath)
        {
            try
            {
                if(settings==null)return false;
                NormalizeScope(settings);
                AlertSoundRouting.EnsureProfiles(settings);
                string directory=Path.GetDirectoryName(Path.GetFullPath(filePath));
                Directory.CreateDirectory(directory);
                string temporaryPath = filePath + ".tmp";
                File.WriteAllText(temporaryPath, new JavaScriptSerializer().Serialize(settings), Encoding.UTF8);
                if (File.Exists(filePath)) File.Replace(temporaryPath, filePath, null);
                else File.Move(temporaryPath, filePath);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (System.Security.SecurityException) { return false; }
        }
        public static void NormalizeScope(WatchSettings settings)
        {
            settings.SelectedSourceIds = (settings.SelectedSourceIds ?? new List<string>()).Where(id => !String.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToList();
            settings.SelectedSourceId = settings.MonitorAllSources ? "" : settings.SelectedSourceIds.FirstOrDefault() ?? "";
        }
    }
}
