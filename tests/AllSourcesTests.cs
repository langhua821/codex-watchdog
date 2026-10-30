using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Web.Script.Serialization;
using CodexWatchdog;
class AllSourcesTests
{
    static int Main()
    {
        string home = Path.Combine(Path.GetTempPath(), "watchdog-all-" + Guid.NewGuid().ToString("N"));
        string day = Path.Combine(home, "sessions", "2026", "10", "24"); Directory.CreateDirectory(day);
        string id = "all-active"; string file = Path.Combine(day, "rollout-active.jsonl");
        JavaScriptSerializer json = new JavaScriptSerializer(); string ts = DateTime.UtcNow.ToString("o");
        using (StreamWriter writer = new StreamWriter(file, false, new UTF8Encoding(false)))
        {
            writer.WriteLine(json.Serialize(new { timestamp = ts, type = "session_meta", payload = new { id = id, source = "vscode", originator = "Codex Desktop" } }));
            writer.WriteLine(json.Serialize(new { timestamp = ts, type = "event_msg", payload = new { type = "task_started", turn_id = "long-active" } }));
            for (int i = 0; i < 24; i++) writer.WriteLine(json.Serialize(new { timestamp = ts, type = "response_item", payload = new { type = "reasoning", text = new string('x', 60000) } }));
        }
        using (CodexSessionSource source = new CodexSessionSource(home))
        {
            MonitorSnapshot state = source.Poll(DateTime.UtcNow).Snapshots.Single();
            if (!state.IsGenerating || state.TurnId != "long-active") { Console.Error.WriteLine("All-chat monitoring lost the latest long-running turn at startup"); return 1; }
        }
        Console.WriteLine("PASS: all-chat startup recognizes latest long-running source beyond the short tail");
        return 0;
    }
}
