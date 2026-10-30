using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CodexWatchdog
{
    public sealed class MainForm : Form
    {
        private readonly WatchSettings settings;
        private readonly WatchEngine engine;
        private CodexSessionSource source;
        private readonly string codexHome;
        private readonly AlarmSound alarm = new AlarmSound();
        private readonly System.Windows.Forms.Timer displayTimer = new System.Windows.Forms.Timer();
        private readonly StatusOrb orb = new StatusOrb();
        private readonly ToolTip tip = new ToolTip();
        private readonly NotifyIcon tray;
        private readonly ToolStripMenuItem pauseItem, stopItem, snoozeItem;
        private readonly Func<bool> presenceProbe;
        private readonly Func<WatchSettings, bool> persistSettings;
        private readonly Action<string, WatchAlert> recordEvent;
        private readonly Dictionary<string, WatchAlert> displayedAlerts = new Dictionary<string, WatchAlert>();
        private HashSet<string> appliedSourceIds;
        private bool appliedAllSources;
        private readonly Dictionary<string, int> scopeBaselines = new Dictionary<string, int>(StringComparer.Ordinal);
        private int scopeEpoch;
        private int allSourcesBaselineEpoch;
        private HashSet<string> previouslyEnabledBeforeAll = new HashSet<string>(StringComparer.Ordinal);
        private System.Threading.Timer pollTimer;
        private SettingsForm settingsForm;
        private List<MonitorSnapshot> latest = new List<MonitorSnapshot>();
        private List<WatchAlert> alerts = new List<WatchAlert>();
        private string health = "正在连接本机运行记录", soundFailure = "", summary = "正在连接", detail = "";
        private string activeSoundSignature = "";
        private DateTime lastPollUtc = DateTime.MinValue, testUntilUtc = DateTime.MinValue, nextSoundAttemptUtc = DateTime.MinValue;
        private int polling, frame, monitorEpoch;
        private AlertKind previewKind = AlertKind.Completed;
        private bool closing, settingsSaveFailed, dragging, moved, suppressClick, appRunning, manuallyHidden, appliedStartup;
        private volatile bool sourceNeedsBaseline;
        private string startupFailure;
        private Point dragPointer, dragWindow;

        public MainForm(WatchSettings settings, bool paused, string codexHome, Func<bool> presenceProbe = null, string startupFailure = "", Func<WatchSettings, bool> persistSettings = null, Action<string, WatchAlert> recordEvent = null)
        {
            this.settings = settings;
            SettingsStore.NormalizeScope(settings);
            appliedAllSources = settings.MonitorAllSources; appliedSourceIds = new HashSet<string>(settings.SelectedSourceIds, StringComparer.Ordinal);
            this.codexHome = codexHome;
            this.presenceProbe = presenceProbe ?? DesktopPresence.IsRunning;
            this.persistSettings = persistSettings ?? SettingsStore.Save;
            this.recordEvent = recordEvent ?? (persistSettings == null ? (Action<string, WatchAlert>)MonitorJournal.Record : delegate { });
            appRunning = this.presenceProbe(); this.startupFailure = startupFailure; appliedStartup = settings.StartWithWindows;
            engine = new WatchEngine(settings); engine.Paused = paused; engine.Reset();
            this.recordEvent("started", null);
            source = new CodexSessionSource(codexHome); source.SelectedSourceId = settings.SelectedSourceId;
            Text = "Codex 守望铃"; Name = "CodexWatchdog"; AccessibleName = Text;
            Font = new Font("Microsoft YaHei UI", 9F);
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            Opacity = 0; // Let Shown start the message-loop timers without flashing a ball when the app is absent.
            float dpi;
            using (Graphics screen = Graphics.FromHwnd(IntPtr.Zero)) dpi = screen.DpiX;
            int diameter = (int)Math.Round(72 * dpi / 96F);
            AutoScaleMode = AutoScaleMode.None;
            MaximizeBox = false; MinimizeBox = false;
            MinimumSize = new Size(diameter, diameter); MaximumSize = new Size(diameter, diameter);
            ClientSize = new Size(diameter, diameter);
            BackColor = Color.FromArgb(27, 36, 49); StartPosition = FormStartPosition.Manual;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            using (GraphicsPath circle = new GraphicsPath()) { circle.AddEllipse(0, 0, diameter, diameter); Region = new Region(circle); }
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 24, area.Top + 100);
            if ((settings.WindowX != -1 || settings.WindowY != -1) && Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new Rectangle(settings.WindowX, settings.WindowY, Width, Height))))
                Location = new Point(settings.WindowX, settings.WindowY);
            orb.Dock = DockStyle.Fill; Controls.Add(orb);
            orb.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging = true; moved = false; suppressClick = false; dragPointer = orb.PointToScreen(e.Location); dragWindow = Location; } };
            orb.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!dragging) return;
                Point pointer = orb.PointToScreen(e.Location);
                int dx = pointer.X - dragPointer.X, dy = pointer.Y - dragPointer.Y;
                if (!moved && Math.Abs(dx) + Math.Abs(dy) < 5) return;
                moved = true; suppressClick = true;
                Rectangle bounds = Screen.FromPoint(pointer).WorkingArea;
                Location = new Point(Math.Max(bounds.Left, Math.Min(bounds.Right - Width, dragWindow.X + dx)), Math.Max(bounds.Top, Math.Min(bounds.Bottom - Height, dragWindow.Y + dy)));
            };
            orb.MouseUp += delegate { dragging = false; if (moved) SavePosition(); };
            orb.Click += delegate { if (suppressClick) { suppressClick = false; return; } if (alerts.Count > 0 || testUntilUtc > DateTime.UtcNow) Confirm(); else TogglePause(); };
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("设置与详细状态…", null, delegate { OpenSettings(); });
            pauseItem = new ToolStripMenuItem("暂停监控", null, delegate { TogglePause(); }); menu.Items.Add(pauseItem);
            stopItem = new ToolStripMenuItem("确认并停止铃声", null, delegate { Confirm(); }); menu.Items.Add(stopItem);
            snoozeItem = new ToolStripMenuItem("稍后提醒", null, delegate { Snooze(); }); menu.Items.Add(snoozeItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("显示 / 隐藏悬浮球", null, delegate { manuallyHidden = Visible; Render(); });
            menu.Items.Add("打开 Codex", null, delegate { OpenCodex(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出守望铃", null, delegate { Close(); });
            tray = new NotifyIcon { Text = "Codex 守望铃", Icon = Icon, ContextMenuStrip = menu, Visible = true };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) OpenSettings(); };
            orb.ContextMenuStrip = menu;
            tip.InitialDelay = 350; tip.ReshowDelay = 100;
            displayTimer.Interval = 1000; displayTimer.Tick += delegate { Render(); };
            Shown += delegate { displayTimer.Start(); pollTimer = new System.Threading.Timer(Poll, null, 0, 2000); SaveSettings(); Render(); };
        }

        private void SavePosition() { settings.WindowX = Location.X; settings.WindowY = Location.Y; SaveSettings(); }
        private void SaveSettings() { settingsSaveFailed = !persistSettings(settings); }
        private void TogglePause() { Interlocked.Increment(ref monitorEpoch); engine.Paused = !engine.Paused; latest = new List<MonitorSnapshot>(); if (!engine.Paused) sourceNeedsBaseline = true; recordEvent(engine.Paused ? "paused" : "resumed", null); alarm.Stop(); testUntilUtc = DateTime.MinValue; Render(); }
        private void Confirm() { foreach (WatchAlert alert in alerts) { recordEvent("confirmed", alert); engine.Acknowledge(alert.Id); } testUntilUtc = DateTime.MinValue; alarm.Stop(); Render(); }
        private void Snooze() { DateTime now = DateTime.UtcNow; foreach (WatchAlert alert in alerts) { recordEvent("snoozed", alert); engine.Snooze(alert.Id, now); } testUntilUtc = DateTime.MinValue; alarm.Stop(); Render(); }
        private void Preview(AlertKind kind) { previewKind = kind; alarm.Stop(); soundFailure = ""; nextSoundAttemptUtc = DateTime.MinValue; testUntilUtc = DateTime.UtcNow.AddSeconds(8); Render(); }
        private void StopPreview() { testUntilUtc = DateTime.MinValue; alarm.Stop(); Render(); }

        private void ApplySettings()
        {
            SettingsStore.NormalizeScope(settings);
            var enabled = new HashSet<string>(settings.SelectedSourceIds, StringComparer.Ordinal);
            if (appliedAllSources != settings.MonitorAllSources || !appliedSourceIds.SetEquals(enabled))
            {
                // Wait for a new read before attaching a chat; an older queued poll
                // must not replay an event from before the user checked it.
                int epoch = Interlocked.Increment(ref scopeEpoch);
                if (!appliedAllSources && settings.MonitorAllSources)
                { allSourcesBaselineEpoch = epoch; previouslyEnabledBeforeAll = new HashSet<string>(appliedSourceIds, StringComparer.Ordinal); }
                foreach (MonitorSnapshot s in latest) if (!appliedAllSources && !appliedSourceIds.Contains(s.SourceId) && (settings.MonitorAllSources || enabled.Contains(s.SourceId))) scopeBaselines[s.SourceId] = epoch;
                foreach (string id in enabled) if (!appliedAllSources && !appliedSourceIds.Contains(id)) scopeBaselines[id] = epoch;
                foreach (string id in scopeBaselines.Keys.ToList()) if (!settings.MonitorAllSources && !enabled.Contains(id)) scopeBaselines.Remove(id);
                appliedAllSources = settings.MonitorAllSources; appliedSourceIds = enabled;
                recordEvent(settings.MonitorAllSources ? "scope-all" : "scope-selected:" + String.Join(",", enabled.OrderBy(id => id)), null);
            }
            if (appliedStartup != settings.StartWithWindows)
            {
                StartupRegistration startup = new StartupRegistration();
                if (!startup.TrySetEnabled(settings.StartWithWindows, Application.ExecutablePath, out startupFailure))
                    settings.StartWithWindows = startup.IsEnabled(Application.ExecutablePath);
                appliedStartup = settings.StartWithWindows;
            }
            if (source.SelectedSourceId != settings.SelectedSourceId)
            {
                source.SelectedSourceId = settings.SelectedSourceId;
            }
            if (!alarm.IsPlaying) { alarm.Stop(); soundFailure = ""; nextSoundAttemptUtc = DateTime.MinValue; }
            SaveSettings(); Render();
        }

        private void Poll(object unused)
        {
            if (closing || Interlocked.Exchange(ref polling, 1) != 0) return;
            try
            {
                int epoch = monitorEpoch;
                int readScopeEpoch = scopeEpoch;
                bool running = presenceProbe();
                if (!running) sourceNeedsBaseline = true;
                else if (sourceNeedsBaseline)
                {
                    source.Dispose(); source = new CodexSessionSource(codexHome); source.SelectedSourceId = settings.SelectedSourceId;
                    sourceNeedsBaseline = false;
                }
                SourcePollResult result = running ? source.Poll(DateTime.UtcNow) : new SourcePollResult { Health = "等待 Codex / ChatGPT 打开" };
                if (!closing && IsHandleCreated) BeginInvoke((Action)delegate
                {
                    if (epoch != monitorEpoch) return;
                    if (appRunning != running)
                    {
                        Interlocked.Increment(ref monitorEpoch);
                        foreach (MonitorSnapshot s in result.Snapshots) s.Transitions.Clear();
                        engine.Reset(); alerts.Clear(); alarm.Stop(); testUntilUtc = DateTime.MinValue;
                        if (running) manuallyHidden = false;
                    }
                    if (engine.Paused) foreach (MonitorSnapshot s in result.Snapshots) s.Transitions.Clear();
                    foreach (MonitorSnapshot s in result.Snapshots)
                    {
                        if (settings.MonitorAllSources && readScopeEpoch < allSourcesBaselineEpoch && !previouslyEnabledBeforeAll.Contains(s.SourceId)) scopeBaselines[s.SourceId] = allSourcesBaselineEpoch;
                        int requiredEpoch;
                        if (scopeBaselines.TryGetValue(s.SourceId, out requiredEpoch) && readScopeEpoch >= requiredEpoch && s.IsAvailable && s.IsCaughtUp)
                        { s.Transitions.Clear(); scopeBaselines.Remove(s.SourceId); }
                    }
                    appRunning = running;
                    if (running) latest = result.Snapshots;
                    health = result.Health; lastPollUtc = DateTime.UtcNow; Render();
                });
            }
            catch (Exception error)
            {
                if (!closing && IsHandleCreated) BeginInvoke((Action)delegate { health = "读取异常：" + error.GetType().Name; foreach (MonitorSnapshot s in latest) s.IsAvailable = false; lastPollUtc = DateTime.UtcNow; Render(); });
            }
            finally { Interlocked.Exchange(ref polling, 0); }
        }

        private void Render()
        {
            if (closing) return;
            DateTime now = DateTime.UtcNow;
            var enabled = new HashSet<string>(settings.SelectedSourceIds, StringComparer.Ordinal);
            List<MonitorSnapshot> selected = latest.Where(s => settings.MonitorAllSources || enabled.Contains(s.SourceId)).ToList();
            List<MonitorSnapshot> monitored = selected.Where(s => !scopeBaselines.ContainsKey(s.SourceId)).ToList();
            if (lastPollUtc != DateTime.MinValue && now - lastPollUtc > TimeSpan.FromSeconds(12)) { selected = selected.Select(CopyUnavailable).ToList(); monitored = monitored.Select(CopyUnavailable).ToList(); health = "状态读取超过 12 秒未更新"; }
            alerts = appRunning ? engine.Update(monitored, now) : new List<WatchAlert>();
            foreach (WatchAlert alert in alerts) if (!displayedAlerts.ContainsKey(alert.Id)) recordEvent("alarm-active", alert);
            var activeIds = new HashSet<string>(alerts.Select(a => a.Id));
            foreach (WatchAlert alert in displayedAlerts.Values) if (!activeIds.Contains(alert.Id)) recordEvent("alarm-cleared-or-snoozed", alert);
            displayedAlerts.Clear(); foreach (WatchAlert alert in alerts) displayedAlerts[alert.Id] = alert;
            bool testing = testUntilUtc > now, ringing = alerts.Count > 0 || testing;
            WatchAlert activeAlert = AlertSoundRouting.SelectAlert(alerts);
            AlertKind soundKind = testing ? previewKind : activeAlert == null ? AlertKind.Completed : activeAlert.Kind;
            SoundProfile sound = AlertSoundRouting.GetProfile(settings, soundKind);
            string signature = (testing ? "preview:" : "alert:") + soundKind + "|" + sound.SoundId + "|" + sound.CustomSoundPath + "|" + settings.VolumePercent;
            if (activeSoundSignature != signature) { alarm.Stop(); soundFailure = ""; nextSoundAttemptUtc = DateTime.MinValue; activeSoundSignature = signature; }
            if (ringing && !string.IsNullOrEmpty(alarm.Failure) && soundFailure != alarm.Failure) { soundFailure = alarm.Failure; nextSoundAttemptUtc = now.AddSeconds(10); }
            if (ringing && !alarm.IsPlaying && now >= nextSoundAttemptUtc)
            {
                try { alarm.Start(settings.VolumePercent, SoundCatalog.Resolve(sound.SoundId, sound.CustomSoundPath)); soundFailure = ""; }
                catch (Exception error) { soundFailure = "铃声不可用：" + error.Message; nextSoundAttemptUtc = now.AddSeconds(10); }
            }
            else if (!ringing) { alarm.Stop(); soundFailure = ""; nextSoundAttemptUtc = DateTime.MinValue; }
            string shortStatus = "待命", stateKind = "idle";
            summary = "空闲，等待下一轮"; detail = "";
            if (ringing)
            {
                if (testing) { shortStatus = "试听"; summary = "正在试听：" + (previewKind == AlertKind.NeedsInput ? "待回答" : previewKind == AlertKind.Completed ? "运行结束" : "执行异常") + "（最多 8 秒）"; }
                else { shortStatus = ShortAlert(activeAlert.Kind); summary = activeAlert.Message; detail = activeAlert.Title; }
                stateKind = "attention";
                if (soundFailure.Length > 0) { stateKind = "error"; shortStatus = "无声音"; summary = soundFailure; }
            }
            else if (!appRunning) { shortStatus = "待启动"; summary = "等待 Codex / ChatGPT 打开"; detail = "守望铃在托盘待命，应用打开后自动监控"; }
            else if (engine.Paused) { shortStatus = "已暂停"; stateKind = "paused"; summary = "监控已暂停"; }
            else if (!settings.MonitorAllSources && enabled.Count == 0) { shortStatus = "未选择"; summary = "尚未勾选监控聊天"; detail = "在托盘设置中多选需要提醒的聊天"; }
            else if (selected.Count == 0) { shortStatus = "连接中"; stateKind = "unknown"; summary = "等待已选聊天记录"; detail = health; }
            else
            {
                MonitorSnapshot recent = selected.OrderByDescending(s => s.LastActivityUtc).First();
                detail = recent.Title;
                if (selected.Any(s => !s.IsAvailable && s.Detail != "正在同步历史运行记录")) { shortStatus = "未连接"; stateKind = "error"; summary = health; }
                else if (selected.Any(s => !s.IsCaughtUp)) { shortStatus = "同步中"; stateKind = "unknown"; summary = "正在读取新记录，暂不判断停止"; }
                else if (selected.Any(s => s.IsGenerating && !s.HasBlockingInput))
                {
                    shortStatus = "运行中"; stateKind = "running"; summary = "正在生成 / 执行";
                    if (settings.StallAlertEnabled && selected.Where(s => s.IsGenerating && !s.HasBlockingInput).All(s => s.LastActivityUtc != DateTime.MinValue && now - s.LastActivityUtc >= TimeSpan.FromMinutes(settings.StallMinutes)))
                    { shortStatus = "久未动"; stateKind = "unknown"; summary = "仍在工作，较久没有新记录（仅显示，不响铃）"; }
                }
                else if (engine.IsConfirmingStop) { shortStatus = "确认中"; stateKind = "unknown"; summary = "正在确认已停稳，自动续跑将取消提醒"; }
                else if (selected.Any(s => s.NeedsInput)) { shortStatus = "待回答"; stateKind = "attention"; summary = "已停下，等待你的回答 / 授权"; }
                else if (recent.HasError) { shortStatus = "有异常"; stateKind = "error"; summary = "本轮停止并出现异常"; }
                else if (recent.ExplicitCompleted) { shortStatus = "已结束"; summary = "本轮已结束"; }
                else if (recent.WasInterrupted) { shortStatus = "已停止"; summary = "本轮生成已中断"; }
                else { shortStatus = "未知"; stateKind = "unknown"; summary = "状态未知：尚未读到足够的开始或结束记录"; }
            }
            bool unhealthy = health.Contains("异常") || health.Contains("不可用") || health.Contains("未运行") || health.Contains("超过");
            if (appRunning && !engine.Paused && !ringing && unhealthy) { stateKind = "error"; shortStatus = "需检查"; summary = health; }
            if (settingsSaveFailed) summary += "；设置未保存";
            if (startupFailure.Length > 0) summary += "；" + startupFailure;
            orb.StatusText = shortStatus; orb.StateKind = stateKind; orb.Alerting = ringing; orb.AnimationFrame = ++frame;
            tip.SetToolTip(orb, Short(summary + "\n" + detail, 220) + (ringing ? "\n单击停止铃声；拖动移动" : "\n单击暂停 / 继续；右键打开设置；拖动移动"));
            tray.Text = Short("Codex 守望铃 · " + shortStatus, 60);
            pauseItem.Text = engine.Paused ? "继续监控" : "暂停监控"; stopItem.Enabled = ringing; snoozeItem.Enabled = alerts.Count > 0;
            bool showOrb = (!settings.ShowOnlyWithDesktopApp || appRunning) && (ringing || !manuallyHidden);
            if (showOrb) { if (Opacity != 1) Opacity = 1; if (!Visible) Show(); if (!TopMost) TopMost = true; } else if (Visible) Hide();
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshStatus(latest, summary, detail, engine.Paused, ringing, startupFailure.Length > 0 ? "启动设置异常：" + startupFailure : health);
            AccessibleDescription = "置顶=" + TopMost + ";响铃=" + alarm.IsPlaying + ";状态=" + shortStatus;
        }

        private static string ShortAlert(AlertKind kind)
        {
            switch (kind) { case AlertKind.Completed: return "已结束"; case AlertKind.NeedsInput: return "待回答"; case AlertKind.Error: return "有异常"; case AlertKind.Stalled: return "请检查"; case AlertKind.Unavailable: return "未连接"; default: return "已停止"; }
        }
        private static string Short(string value, int size) { value = value ?? ""; return value.Length <= size ? value : value.Substring(0, size - 1) + "…"; }
        private static MonitorSnapshot CopyUnavailable(MonitorSnapshot s)
        {
            return new MonitorSnapshot { SourceId = s.SourceId, Title = s.Title, TurnId = s.TurnId, Fingerprint = s.Fingerprint, AttentionId = s.AttentionId, ObservedUtc = s.ObservedUtc, LastActivityUtc = s.LastActivityUtc, EndedUtc = s.EndedUtc, AttentionUtc = s.AttentionUtc, IsAvailable = false, IsGenerating = s.IsGenerating, NeedsInput = s.NeedsInput, HasBlockingInput = s.HasBlockingInput, IsCaughtUp = false, HasError = s.HasError, ExplicitCompleted = s.ExplicitCompleted, WasInterrupted = s.WasInterrupted, Detail = s.Detail };
        }
        private void OpenSettings()
        {
            if (settingsForm != null && !settingsForm.IsDisposed) { settingsForm.WindowState = FormWindowState.Normal; settingsForm.Show(); settingsForm.Activate(); return; }
            settingsForm = new SettingsForm(settings, ApplySettings, Preview, StopPreview, TogglePause, Confirm);
            settingsForm.FormClosed += delegate { settingsForm = null; };
            settingsForm.Show(); Render(); settingsForm.Activate();
        }
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        private void OpenCodex()
        {
            foreach (string name in new[] { "ChatGPT", "Codex" }) foreach (Process p in Process.GetProcessesByName(name)) using (p) { if (p.MainWindowHandle == IntPtr.Zero) continue; ShowWindow(p.MainWindowHandle, 9); SetForegroundWindow(p.MainWindowHandle); return; }
            MessageBox.Show("请先打开 Codex 桌面应用。", "守望铃");
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            closing = true; displayTimer.Stop(); if (pollTimer != null) pollTimer.Dispose();
            recordEvent("exited", null);
            alarm.Dispose(); if (settingsForm != null) settingsForm.Close();
            tray.Visible = false; tray.Dispose(); tip.Dispose(); SavePosition();
            base.OnFormClosing(e);
        }
    }
}
