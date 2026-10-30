using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CodexWatchdog
{
    public sealed class SettingsForm : Form
    {
        private readonly WatchSettings settings;
        private readonly Action apply;
        private readonly ComboBox sounds, categories;
        private readonly Button targets;
        private readonly CheckedListBox targetChoices;
        private readonly ToolStripDropDown targetPopup;
        private readonly Label status, detail, healthLabel, customLabel, volumeText;
        private readonly CheckBox inactivity;
        private readonly NumericUpDown minutes, snooze;
        private readonly TrackBar volume;
        private readonly Button pause, stop;
        private bool updating;
        private string targetSignature = "";
        private AlertKind editingKind = AlertKind.NeedsInput;
        private readonly Color ink = Color.FromArgb(26, 39, 61), muted = Color.FromArgb(106, 119, 139), blue = Color.FromArgb(77, 106, 221);

        public SettingsForm(WatchSettings settings, Action apply, Action<AlertKind> preview, Action stopPreview, Action togglePause, Action confirm)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
            this.settings = settings; this.apply = apply;
            AlertSoundRouting.EnsureProfiles(settings);
            Text = "守望铃 1.4.1 · 设置与状态"; Name = "WatchdogSettings"; AccessibleName = Text;
            Font = new Font("Microsoft YaHei UI", 9F); ForeColor = ink; BackColor = Color.FromArgb(246, 248, 252);
            ClientSize = new Size(428, 714); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            AddLabel("守望铃设置", 22, 18, 380, 32, 17, ink);
            AddLabel("右下角托盘入口 · 修改后自动保存", 22, 57, 380, 22, 8.5F, muted);
            AddLabel("监控聊天（可多选，仅勾选的聊天会响）", 22, 91, 384, 22, 9, ink);
            targets = AddButton("", 22, 115, 384, false); targets.Height = 28; targets.AccessibleName = "监控聊天多选";
            targetChoices = new CheckedListBox { CheckOnClick = true, BorderStyle = BorderStyle.None, IntegralHeight = false, Size = new Size(376, 212), Font = Font, HorizontalScrollbar = true, AccessibleName = "监控聊天勾选列表" };
            ToolStripControlHost host = new ToolStripControlHost(targetChoices) { AutoSize = false, Size = targetChoices.Size, Margin = Padding.Empty, Padding = Padding.Empty };
            targetPopup = new ToolStripDropDown { AutoSize = false, Size = new Size(384, 220), Padding = new Padding(4), AutoClose = true };
            targetPopup.Items.Add(host);
            targets.Click += delegate
            {
                if (targetPopup.Visible) { targetPopup.Close(); return; }
                float scale = targets.Width / 384F;
                int padding = Math.Max(1, (int)Math.Round(4 * scale));
                targetPopup.Size = new Size(targets.Width, (int)Math.Round(220 * scale)); targetPopup.Padding = new Padding(padding);
                host.Size = targetChoices.Size = new Size(targetPopup.Width - 2 * padding, targetPopup.Height - 2 * padding);
                targetPopup.Show(targets, new Point(0, targets.Height));
            };
            targetChoices.ItemCheck += TargetCheckChanged;
            if (settings.SelectedSourceIds == null) settings.SelectedSourceIds = new List<string>();
            UpdateTargetButton();
            Panel stateCard = new Panel { Location = new Point(22, 157), Size = new Size(384, 72), BackColor = Color.White };
            status = new Label { Location = new Point(12, 9), Size = new Size(360, 23), Font = new Font(Font.FontFamily, 10F, FontStyle.Bold), ForeColor = blue, AutoEllipsis = true };
            detail = new Label { Location = new Point(12, 36), Size = new Size(360, 24), ForeColor = muted, AutoEllipsis = true };
            stateCard.Controls.AddRange(new Control[] { status, detail }); Controls.Add(stateCard);
            inactivity = new CheckBox { Text = "显示无活动状态（不响铃）", Checked = settings.StallAlertEnabled, Location = new Point(22, 244), Size = new Size(260, 27), AccessibleName = "无活动提醒开关" };
            minutes = new NumericUpDown { Minimum = 1, Maximum = 180, Value = settings.StallMinutes, Enabled = settings.StallAlertEnabled, Location = new Point(286, 246), Size = new Size(68, 26), AccessibleName = "无活动提醒分钟" };
            Controls.Add(inactivity); Controls.Add(minutes); AddLabel("分钟", 362, 247, 44, 22, 9, muted);
            AddLabel("新待答问题立即提醒；结束停稳4秒后提醒", 22, 277, 384, 20, 8.2F, muted);
            inactivity.CheckedChanged += delegate { minutes.Enabled = inactivity.Checked; if (!updating) { settings.StallAlertEnabled = inactivity.Checked; apply(); } };
            minutes.ValueChanged += delegate { if (!updating) { settings.StallMinutes = (int)minutes.Value; apply(); } };
            AddLabel("提醒类型（各类独立保存铃声）", 22, 312, 384, 22, 9, ink);
            categories = new ComboBox { Location = new Point(22, 337), Size = new Size(384, 28), DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "提醒类型" };
            categories.Items.AddRange(new object[] { "待回答 / 授权", "运行结束 / 中断", "停止并出现异常（沿用原无活动铃声）" }); categories.SelectedIndex = 0; Controls.Add(categories);
            AddLabel("提醒铃声", 22, 376, 384, 22, 9, ink);
            sounds = new ComboBox { Location = new Point(22, 401), Size = new Size(258, 28), DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 384, MaxDropDownItems = 12, IntegralHeight = false, AccessibleName = "提醒铃声" };
            List<SoundOption> options = SoundCatalog.BuiltIn(); foreach (SoundOption option in options) sounds.Items.Add(option);
            sounds.Items.Add(new SoundOption { Id = "custom", Name = "自选本地铃声", FilePath = settings.CustomSoundPath });
            Controls.Add(sounds);
            Button choose = AddButton("选择文件…", 292, 399, 114, false);
            choose.Click += delegate { ChooseSound(); };
            customLabel = AddLabel("", 22, 434, 384, 22, 8.2F, muted); RefreshSoundControls();
            categories.SelectedIndexChanged += delegate
            {
                if (updating) return;
                editingKind = categories.SelectedIndex == 0 ? AlertKind.NeedsInput : categories.SelectedIndex == 1 ? AlertKind.Completed : AlertKind.Stalled;
                RefreshSoundControls();
            };
            sounds.SelectedIndexChanged += delegate
            {
                if (updating) return;
                SoundOption option = sounds.SelectedItem as SoundOption;
                if (option == null) return;
                SoundProfile profile = AlertSoundRouting.GetProfile(settings, editingKind);
                if (option.Id == "custom" && string.IsNullOrEmpty(profile.CustomSoundPath)) { ChooseSound(); return; }
                profile.SoundId = option.Id; UpdateSoundLabel(); apply();
            };
            AddLabel("音量", 22, 469, 48, 24, 9, ink);
            volume = new TrackBar { Minimum = 5, Maximum = 100, Value = settings.VolumePercent, TickStyle = TickStyle.None, Location = new Point(74, 460), Size = new Size(260, 38), AccessibleName = "铃声音量" };
            volumeText = AddLabel(settings.VolumePercent + "%", 349, 468, 57, 24, 9, muted); Controls.Add(volume);
            volume.ValueChanged += delegate { settings.VolumePercent = volume.Value; volumeText.Text = volume.Value + "%"; apply(); };
            AddButton("试听 8 秒", 22, 507, 116, false).Click += delegate { preview(editingKind); };
            stop = AddButton("停止铃声", 150, 507, 116, false); stop.Click += delegate { stopPreview(); confirm(); };
            pause = AddButton("暂停监控", 278, 507, 128, true); pause.Click += delegate { togglePause(); };
            AddLabel("稍后提醒", 22, 553, 92, 23, 8.5F, muted);
            snooze = new NumericUpDown { Minimum = 1, Maximum = 60, Value = settings.SnoozeMinutes, Location = new Point(116, 551), Size = new Size(58, 25), AccessibleName = "稍后提醒分钟" }; Controls.Add(snooze);
            AddLabel("分钟", 181, 553, 44, 23, 8.5F, muted);
            snooze.ValueChanged += delegate { settings.SnoozeMinutes = (int)snooze.Value; apply(); };
            healthLabel = AddLabel("", 232, 554, 174, 23, 8F, muted);
            AddLabel("启动与显示", 22, 595, 384, 22, 9, ink);
            CheckBox startup = new CheckBox { Text = "登录 Windows 时自动启动", Checked = settings.StartWithWindows, Location = new Point(22, 622), Size = new Size(384, 27), AccessibleName = "开机自启动开关" };
            CheckBox follow = new CheckBox { Text = "仅在 Codex / ChatGPT 运行时显示悬浮球", Checked = settings.ShowOnlyWithDesktopApp, Location = new Point(22, 654), Size = new Size(384, 27), AccessibleName = "跟随应用显示开关" };
            Controls.Add(startup); Controls.Add(follow);
            startup.CheckedChanged += delegate
            {
                if (updating) return; settings.StartWithWindows = startup.Checked; apply();
                if (startup.Checked != settings.StartWithWindows) { updating = true; startup.Checked = settings.StartWithWindows; updating = false; }
            };
            follow.CheckedChanged += delegate { if (!updating) { settings.ShowOnlyWithDesktopApp = follow.Checked; apply(); } };
            AddLabel("关闭应用后在托盘待命；后台或最小化仍显示", 22, 686, 384, 20, 8.2F, muted);
            ResumeLayout(false);
        }

        private Label AddLabel(string text, int x, int y, int width, int height, float size, Color color)
        {
            Label label = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), ForeColor = color, Font = new Font(Font.FontFamily, size), AutoEllipsis = true };
            Controls.Add(label); return label;
        }
        private Button AddButton(string text, int x, int y, int width, bool primary)
        {
            Button button = new Button { Text = text, AccessibleName = text, Location = new Point(x, y), Size = new Size(width, 32), FlatStyle = FlatStyle.Flat, BackColor = primary ? blue : Color.White, ForeColor = primary ? Color.White : ink, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = primary ? blue : Color.FromArgb(222, 227, 238); Controls.Add(button); return button;
        }
        private void ChooseSound()
        {
            SoundProfile profile = AlertSoundRouting.GetProfile(settings, editingKind);
            using (OpenFileDialog dialog = new OpenFileDialog { Title = "选择提醒铃声", Filter = "铃声音频 (*.wav;*.mp3)|*.wav;*.mp3", CheckFileExists = true, Multiselect = false })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    RefreshSoundControls(); return;
                }
                profile.CustomSoundPath = dialog.FileName; profile.SoundId = "custom";
                RefreshSoundControls(); apply();
            }
        }
        private void RefreshSoundControls()
        {
            SoundProfile profile = AlertSoundRouting.GetProfile(settings, editingKind);
            updating = true;
            try
            {
                int selected = -1;
                for (int i = 0; i < sounds.Items.Count; i++) if (String.Equals(((SoundOption)sounds.Items[i]).Id, profile.SoundId, StringComparison.OrdinalIgnoreCase)) selected = i;
                if (selected < 0) { sounds.Items.Add(new SoundOption { Id = profile.SoundId, Name = "未找到已选铃声：" + profile.SoundId }); selected = sounds.Items.Count - 1; }
                sounds.SelectedIndex = selected; UpdateSoundLabel();
            }
            finally { updating = false; }
        }
        private void UpdateSoundLabel() { SoundProfile profile = AlertSoundRouting.GetProfile(settings, editingKind); customLabel.Text = String.Equals(profile.SoundId, "custom", StringComparison.OrdinalIgnoreCase) ? "本地文件：" + Path.GetFileName(profile.CustomSoundPath) : "内置真实音频 · CC0 授权，可试听切换"; }
        public void RefreshStatus(List<MonitorSnapshot> snapshots, string summary, string sourceDetail, bool paused, bool ringing, string health)
        {
            status.Text = summary; detail.Text = sourceDetail.Length == 0 ? "只读本机记录 · 后台和最小化也可监控" : sourceDetail;
            pause.Text = paused ? "继续监控" : "暂停监控"; pause.AccessibleName = pause.Text; stop.Enabled = ringing;
            healthLabel.Text = health.Contains("启动设置异常") ? "启动设置未生效" : health.Contains("异常") || health.Contains("不可用") ? "读取状态需检查" : "修改后自动保存";
            string signature = string.Join("|", snapshots.Select(s => s.SourceId + ":" + s.Title).OrderBy(s => s)) + ";" + settings.MonitorAllSources + ";" + string.Join("|", settings.SelectedSourceIds.OrderBy(id => id));
            if (signature == targetSignature || targetPopup.Visible || categories.DroppedDown || sounds.DroppedDown) return;
            targetSignature = signature; updating = true;
            string selectedId = targetChoices.SelectedItem is TargetItem ? ((TargetItem)targetChoices.SelectedItem).Id : "";
            int top = targetChoices.TopIndex;
            try
            {
                targetChoices.BeginUpdate(); targetChoices.Items.Clear();
                targetChoices.Items.Add(new TargetItem("", "全部聊天（包括后台巡查）"), settings.MonitorAllSources);
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (MonitorSnapshot snapshot in snapshots.OrderByDescending(s => s.LastActivityUtc))
                    if (ids.Add(snapshot.SourceId)) targetChoices.Items.Add(new TargetItem(snapshot.SourceId, snapshot.Title), settings.MonitorAllSources || settings.SelectedSourceIds.Contains(snapshot.SourceId));
                foreach (string id in settings.SelectedSourceIds)
                    if (ids.Add(id)) targetChoices.Items.Add(new TargetItem(id, "已勾选聊天（暂不可用）"), true);
                for (int i = 0; i < targetChoices.Items.Count; i++) if (((TargetItem)targetChoices.Items[i]).Id == selectedId) targetChoices.SelectedIndex = i;
                if (targetChoices.Items.Count > 0) targetChoices.TopIndex = Math.Min(Math.Max(0, top), targetChoices.Items.Count - 1);
                UpdateTargetButton();
            }
            finally { targetChoices.EndUpdate(); updating = false; }
        }
        private void UpdateTargetButton()
        {
            targets.Text = settings.MonitorAllSources ? "全部聊天（包括后台巡查）  ▾" : settings.SelectedSourceIds.Count == 0 ? "未选择聊天（不会响铃）  ▾" : "已选择 " + settings.SelectedSourceIds.Count + " 个聊天  ▾";
        }
        private void TargetCheckChanged(object sender, ItemCheckEventArgs e)
        {
            if (updating) return;
            bool enabled = e.NewValue == CheckState.Checked;
            updating = true;
            try
            {
                if (e.Index == 0)
                {
                    settings.MonitorAllSources = enabled; settings.SelectedSourceIds.Clear();
                    for (int i = 1; i < targetChoices.Items.Count; i++) targetChoices.SetItemChecked(i, enabled);
                }
                else
                {
                    if (settings.MonitorAllSources)
                    {
                        settings.SelectedSourceIds = targetChoices.Items.Cast<TargetItem>().Where(item => item.Id.Length > 0).Select(item => item.Id).ToList();
                        settings.MonitorAllSources = false; targetChoices.SetItemChecked(0, false);
                    }
                    string id = ((TargetItem)targetChoices.Items[e.Index]).Id;
                    settings.SelectedSourceIds.RemoveAll(value => value == id);
                    if (enabled) settings.SelectedSourceIds.Add(id);
                }
                UpdateTargetButton();
            }
            finally { updating = false; }
            apply();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && targetPopup != null) targetPopup.Dispose();
            base.Dispose(disposing);
        }
        private sealed class TargetItem
        {
            public string Id; private readonly string title;
            public TargetItem(string id, string title) { Id = id; this.title = title; }
            public override string ToString() { return title; }
        }
    }
}
