using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;

namespace CodexWatchdog
{
    public sealed class SoundOption
    {
        public string Id;
        public string Name;
        public string FilePath;
        public override string ToString() { return Name; }
    }
    public static class SoundCatalog
    {
        public static List<SoundOption> BuiltIn()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds");
            List<SoundOption> options = new List<SoundOption> {
                new SoundOption { Id = "gentle", Name = "轻快电子旋律 · 8 秒", FilePath = Path.Combine(folder, "gentle.wav") },
                new SoundOption { Id = "chimes", Name = "晨间钢琴节拍 · 17 秒", FilePath = Path.Combine(folder, "chimes.wav") },
                new SoundOption { Id = "warm", Name = "活力合成器鼓点 · 21 秒", FilePath = Path.Combine(folder, "warm.wav") }
            };
            string catalog = Path.Combine(folder, "extras.json");
            try
            {
                FileInfo info = new FileInfo(catalog);
                if (!info.Exists || info.Length > 65536) return options;
                JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 65536 };
                object[] rows = json.DeserializeObject(File.ReadAllText(catalog, Encoding.UTF8)) as object[];
                if (rows == null) return options;
                HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gentle", "chimes", "warm", "custom" };
                for (int i = 0; i < rows.Length && i < 64; i++)
                {
                    Dictionary<string, object> row = rows[i] as Dictionary<string, object>; object value;
                    string id = row != null && row.TryGetValue("id", out value) ? value as string : null;
                    string name = row != null && row.TryGetValue("name", out value) ? value as string : null;
                    string file = row != null && row.TryGetValue("file", out value) ? value as string : null;
                    if (String.IsNullOrWhiteSpace(id) || id.Length > 64 || String.IsNullOrWhiteSpace(name) || name.Length > 100 || String.IsNullOrWhiteSpace(file)) continue;
                    string extension = Path.GetExtension(file);
                    if (file != Path.GetFileName(file) || file.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 ||
                        (!String.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase) && !String.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase)) || !ids.Add(id)) continue;
                    options.Add(new SoundOption { Id = id, Name = name, FilePath = Path.Combine(folder, file) });
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            return options;
        }
        public static string Resolve(string soundId, string customPath)
        {
            if (String.Equals(soundId, "custom", StringComparison.OrdinalIgnoreCase))
            {
                if (String.IsNullOrWhiteSpace(customPath)) throw new InvalidOperationException("请先选择本地铃声文件。");
                return Path.GetFullPath(customPath);
            }
            List<SoundOption> options = BuiltIn();
            foreach (SoundOption option in options)
                if (String.Equals(soundId, option.Id, StringComparison.OrdinalIgnoreCase)) return option.FilePath;
            throw new InvalidOperationException("找不到已选的内置铃声，请在设置中重新选择。");
        }
    }
}
