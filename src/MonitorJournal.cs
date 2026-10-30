using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace CodexWatchdog
{
    // Only local status metadata, never conversation bodies or tool arguments.
    public static class MonitorJournal
    {
        public static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexWatchdog", "monitor-events.jsonl");
        public static void Record(string action, WatchAlert alert) { Write(FilePath, action, alert); }
        public static void Write(string file, string action, WatchAlert alert)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                if (File.Exists(file) && new FileInfo(file).Length > 512 * 1024)
                {
                    string previous = file + ".previous";
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(file, previous);
                }
                var entry = new { utc = DateTime.UtcNow.ToString("o"), version = Assembly.GetExecutingAssembly().GetName().Version.ToString(), action = action,
                    sourceId = alert == null ? "" : alert.SourceId, title = alert == null ? "" : alert.Title,
                    turnId = alert == null ? "" : alert.TurnId, transitionId = alert == null ? "" : alert.TransitionId,
                    turnTrigger = alert == null ? "" : alert.TurnTrigger,
                    alertId = alert == null ? "" : alert.Id, kind = alert == null ? "" : alert.Kind.ToString() };
                File.AppendAllText(file, new JavaScriptSerializer().Serialize(entry) + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
