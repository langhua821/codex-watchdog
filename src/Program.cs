using System;
using System.Threading;
using System.Windows.Forms;

namespace CodexWatchdog
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\CodexWatchdog.MainWindow.v1", out created))
            {
                if (!created) { MessageBox.Show("守望铃已经运行，请查看桌面浮窗或任务栏。", "Codex 守望铃"); return; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                WatchSettings settings = SettingsStore.Load();
                bool paused = Array.IndexOf(args, "--paused") >= 0;
                int threadArgument = Array.IndexOf(args, "--thread");
                if (threadArgument >= 0 && threadArgument + 1 < args.Length)
                {
                    settings.MonitorAllSources = false; settings.SelectedSourceIds.Clear();
                    settings.SelectedSourceIds.Add(args[threadArgument + 1]); SettingsStore.NormalizeScope(settings);
                }
                string codexHome = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
                int homeArgument = Array.IndexOf(args, "--codex-home");
                if (homeArgument >= 0 && homeArgument + 1 < args.Length) codexHome = System.IO.Path.GetFullPath(args[homeArgument + 1]);
                string startupError;
                StartupRegistration startup = new StartupRegistration();
                if (!startup.TrySetEnabled(settings.StartWithWindows, Application.ExecutablePath, out startupError))
                    settings.StartWithWindows = startup.IsEnabled(Application.ExecutablePath);
                Application.Run(new MainForm(settings, paused, codexHome, null, startupError));
            }
        }
    }
}
