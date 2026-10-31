using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CodexWatchdog
{
    // A visible source label for the same alert list used by the sound player.
    // This window never takes focus away from a chat or an open settings menu.
    public sealed class AlertSourcePopup : Form
    {
        readonly Panel entries = new Panel { AutoScroll = true, Dock = DockStyle.Fill };
        string signature = "";
        int contentHeight;
        readonly Font titleFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        readonly Font reasonFont = new Font("Microsoft YaHei UI", 8.5F);
        public AlertSourcePopup()
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual; BackColor = Color.FromArgb(30, 41, 57);
            Padding = new Padding(10); Font = reasonFont;
            Text = "提醒来源"; AccessibleName = Text; Controls.Add(entries);
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var parameters = base.CreateParams; parameters.ExStyle |= 0x08000000 | 0x00000080; return parameters; }
        }
        public void RefreshAlerts(IList<WatchAlert> alerts, Rectangle anchor)
        {
            if (alerts == null || alerts.Count == 0) { Hide(); return; }
            Rectangle area = Screen.FromRectangle(anchor).WorkingArea;
            float scale = Math.Max(1F, anchor.Width / 72F);
            int width = Math.Min((int)(286 * scale), Math.Max(1, area.Width - 16));
            Padding = new Padding((int)(10 * scale));
            string next = width + "|" + String.Join("\n", alerts.Select(a => a.Id + "|" + a.Title + "|" + a.Message));
            if (signature != next)
            {
                signature = next;
                entries.SuspendLayout();
                entries.AutoScrollPosition = Point.Empty;
                foreach (Control control in entries.Controls.Cast<Control>().ToArray()) control.Dispose();
                int y = 0, textWidth = Math.Max(20, width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                foreach (WatchAlert alert in alerts)
                {
                    string title = String.IsNullOrWhiteSpace(alert.Title) ? "未命名聊天" : alert.Title;
                    var source = new Label { Text = title, AccessibleName = "来源聊天：" + title, ForeColor = Color.FromArgb(250, 207, 135),
                        Location = new Point(0, y), Width = textWidth, AutoSize = false, UseMnemonic = false };
                    source.Font = titleFont;
                    source.Height = TextRenderer.MeasureText(title, source.Font, new Size(textWidth, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
                    entries.Controls.Add(source); y += source.Height;
                    var reason = new Label { Text = alert.Message, AccessibleName = alert.Message, ForeColor = Color.FromArgb(221, 231, 242),
                        Location = new Point(0, y), Width = textWidth, AutoSize = false, UseMnemonic = false, Font = reasonFont };
                    reason.Height = TextRenderer.MeasureText(reason.Text, reason.Font, new Size(textWidth, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
                    entries.Controls.Add(reason); y += reason.Height + (int)(10 * scale);
                }
                contentHeight = y + Padding.Vertical;
                entries.AutoScrollMinSize = new Size(0, y);
                entries.ResumeLayout();
                AccessibleDescription = String.Join("；", alerts.Select(a => a.Title + "：" + a.Message));
            }
            int height = Math.Min(contentHeight, Math.Min((int)(300 * scale), area.Height - 16));
            int x = anchor.Left - width - 8;
            if (x < area.Left + 8) x = anchor.Right + 8;
            x = Math.Max(area.Left + 8, Math.Min(x, area.Right - width - 8));
            int top = Math.Max(area.Top + 8, Math.Min(anchor.Top, area.Bottom - height - 8));
            Rectangle bounds = new Rectangle(x, top, width, Math.Max(1, height));
            if (Bounds != bounds) Bounds = bounds;
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { titleFont.Dispose(); reasonFont.Dispose(); }
        }
    }
}
