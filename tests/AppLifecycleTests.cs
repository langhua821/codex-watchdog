using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using CodexWatchdog;
class AppLifecycleTests
{
    static JavaScriptSerializer json = new JavaScriptSerializer();
    static object Field(object target, string name) { return target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target); }
    static void Call(object target, string name, params object[] args) { target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Wait(Func<bool> condition)
    {
        DateTime limit = DateTime.UtcNow.AddSeconds(15);
        while (!condition()) { if (DateTime.UtcNow > limit) throw new Exception("UI state did not settle"); Application.DoEvents(); Thread.Sleep(15); }
        Application.DoEvents();
    }
    static void Event(string file, string type, string turn, DateTime when)
    {
        File.AppendAllText(file, json.Serialize(new { timestamp = when.ToString("o"), type = "event_msg", payload = new { type = type, turn_id = turn, call_id = "historic-question" } }) + "\n");
    }
    [STAThread] static int Main()
    {
        string home = Path.Combine(Path.GetTempPath(), "watchdog-life-" + Guid.NewGuid().ToString("N"));
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string day = Path.Combine(home, "sessions", "2026", "10", "24"); Directory.CreateDirectory(day);
            string file = Path.Combine(day, "rollout-desktop.jsonl");
            File.WriteAllText(file, json.Serialize(new { type = "session_meta", payload = new { id = "desktop", originator = "Codex Desktop", source = "vscode" } }) + "\n");
            Event(file, "task_started", "first", DateTime.UtcNow.AddSeconds(-10));
            bool present = false;
            WatchSettings settings = new WatchSettings { StartWithWindows = false, VolumePercent = 0 };
            using (MainForm form = new MainForm(settings, false, home, () => present, "", value => true))
            {
                form.Location = new Point(-20000, -20000); form.Show();
                Wait(() => !form.Visible && form.AccessibleDescription != null);
                Check(((NotifyIcon)Field(form, "tray")).Visible, "hidden startup must retain tray and message loop");
                present = true; Call(form, "Poll", new object[] { null });
                Wait(() => form.Visible && form.AccessibleDescription.Contains("运行中"));
                Event(file, "task_complete", "first", DateTime.UtcNow); Call(form, "Poll", new object[] { null });
                Wait(() => form.AccessibleDescription.Contains("已结束") && ((AlarmSound)Field(form, "alarm")).IsPlaying);
                Check(((AlarmSound)Field(form, "alarm")).IsPlaying, "live completion must start alarm");
                Event(file, "task_started", "resumed", DateTime.UtcNow); Call(form, "Poll", new object[] { null });
                Wait(() => form.AccessibleDescription.Contains("运行中") && !((AlarmSound)Field(form, "alarm")).IsPlaying);
                // Hold a completed poll callback in the UI queue across pause/resume.
                System.Threading.Timer worker = (System.Threading.Timer)Field(form, "pollTimer");
                worker.Change(Timeout.Infinite, Timeout.Infinite);
                while ((int)Field(form, "polling") != 0) Thread.Sleep(10);
                Application.DoEvents();
                Event(file, "task_complete", "resumed", DateTime.UtcNow);
                Call(form, "Poll", new object[] { null }); // BeginInvoke queued; deliberately no message pump.
                Call(form, "TogglePause"); Call(form, "TogglePause");
                Application.DoEvents();
                Check(!((AlarmSound)Field(form, "alarm")).IsPlaying, "stale epoch completion must be discarded across pause/resume");
                Call(form, "Poll", new object[] { null }); Application.DoEvents();
                DateTime quietUntil = DateTime.UtcNow.AddSeconds(5.2);
                while (DateTime.UtcNow < quietUntil)
                {
                    Call(form, "Poll", new object[] { null }); Application.DoEvents();
                    Check(!((AlarmSound)Field(form, "alarm")).IsPlaying, "fresh post-resume baseline must not replay queued historical completion");
                    Thread.Sleep(60);
                }
                worker.Change(0, 2000);
                present = false; Call(form, "Poll", new object[] { null });
                Wait(() => !form.Visible && !((bool)Field(form, "appRunning")));
                Check(!((AlarmSound)Field(form, "alarm")).IsPlaying, "desktop exit must stop alarm");
                Check(((System.Collections.ICollection)Field(form, "alerts")).Count == 0, "desktop exit must clear pending alerts");
                Call(form, "Preview", AlertKind.Completed); Check(!form.Visible, "manual preview must not expose orb without desktop"); Call(form, "StopPreview");
                Call(form, "TogglePause");
                Event(file, "task_started", "historic", DateTime.UtcNow.AddSeconds(-5));
                Event(file, "request_user_input", "historic", DateTime.UtcNow.AddSeconds(-5));
                present = true; Call(form, "Poll", new object[] { null });
                Wait(() => form.Visible && ((bool)Field(form, "appRunning")));
                Check(((WatchEngine)Field(form, "engine")).Paused, "manual pause must survive desktop reopening");
                Call(form, "TogglePause"); Check(!((AlarmSound)Field(form, "alarm")).IsPlaying, "historical request must remain quiet after resume");
                Event(file, "user_message", "historic", DateTime.UtcNow);
                Event(file, "task_started", "second", DateTime.UtcNow); Call(form, "Poll", new object[] { null });
                Wait(() => form.AccessibleDescription.Contains("运行中"));
                Event(file, "task_complete", "second", DateTime.UtcNow); Call(form, "Poll", new object[] { null });
                Wait(() => ((AlarmSound)Field(form, "alarm")).IsPlaying);
                Call(form, "Confirm"); Check(!((AlarmSound)Field(form, "alarm")).IsPlaying, "confirmed new alarm must stop");
                present = false; Call(form, "Poll", new object[] { null }); Wait(() => !form.Visible);
                form.Close();
            }
            using (SettingsForm settingsForm = new SettingsForm(new WatchSettings(), () => {}, kind => {}, () => {}, () => {}, () => {}))
            {
                settingsForm.Location = new Point(-20000, -20000); settingsForm.StartPosition = FormStartPosition.Manual; settingsForm.Show(); Application.DoEvents();
                Check(settingsForm.Controls.OfType<CheckBox>().Count(c => c.Checked && (c.AccessibleName == "开机自启动开关" || c.AccessibleName == "跟随应用显示开关")) == 2, "new controls must default enabled");
                using (Bitmap preview = new Bitmap(settingsForm.Width, settingsForm.Height)) { settingsForm.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size)); preview.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-settings.png")); }
                settingsForm.Close();
            }
            Console.WriteLine("PASS: real offscreen forms hide on startup, follow desktop transitions, stop/rearm alarms, preserve pause, reject queued stale-epoch completion after resume, and expose settings controls"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            string full = Path.GetFullPath(home), temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("watchdog-life-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
        }
    }
}
