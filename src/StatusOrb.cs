using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodexWatchdog
{
    public sealed class StatusOrb : Button
    {
        private string statusText = "待命";
        private string stateKind = "idle";
        private bool alerting, hovered, pressed;
        private int animationFrame;
        public string StatusText { get { return statusText; } set { statusText = value ?? "待命"; AccessibleName = "会话提醒：" + statusText; Invalidate(); } }
        public string StateKind { get { return stateKind; } set { stateKind = value ?? "unknown"; Invalidate(); } }
        public bool Alerting { get { return alerting; } set { alerting = value; Invalidate(); } }
        public int AnimationFrame { get { return animationFrame; } set { animationFrame = value; if (alerting) Invalidate(); } }

        public StatusOrb()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Size = new Size(72, 72);
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
            StatusText = statusText;
            UseVisualStyleBackColor = false;
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
        protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
        private Color Accent()
        {
            switch (stateKind)
            {
                case "running": return Color.FromArgb(87, 193, 207);
                case "attention": return Color.FromArgb(242, 183, 85);
                case "error": return Color.FromArgb(235, 128, 123);
                case "paused": return Color.FromArgb(166, 177, 190);
                default: return Color.FromArgb(153, 180, 202);
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            float scale = Math.Min(Width, Height) / 72f;
            if (scale <= 0) return;
            GraphicsState saved = g.Save();
            g.TranslateTransform((Width - 72 * scale) / 2f, (Height - 72 * scale) / 2f);
            g.ScaleTransform(scale, scale);
            Color accent = Accent();
            using (SolidBrush background = new SolidBrush(Color.FromArgb(27, 36, 49))) g.FillEllipse(background, 0, 0, 72, 72);
            RectangleF body = new RectangleF(3, 3, 66, 66);
            Color top = pressed ? Color.FromArgb(36, 47, 62) : hovered ? Color.FromArgb(63, 80, 99) : Color.FromArgb(51, 66, 84);
            using (LinearGradientBrush fill = new LinearGradientBrush(body, top, Color.FromArgb(24, 33, 47), LinearGradientMode.Vertical)) g.FillEllipse(fill, body);
            using (Pen edge = new Pen(Color.FromArgb(105, 190, 207, 223), 1f)) g.DrawEllipse(edge, body);
            int ringAlpha = alerting ? ((animationFrame & 1) == 0 ? 220 : 125) : hovered ? 150 : 90;
            using (Pen ring = new Pen(Color.FromArgb(ringAlpha, accent), alerting ? 2f : 1.2f)) g.DrawEllipse(ring, 6, 6, 60, 60);
            float offset = pressed ? 1f : 0f;
            using (Pen icon = new Pen(accent, 2f))
            {
                icon.StartCap = LineCap.Round; icon.EndCap = LineCap.Round;
                if (stateKind == "paused") { g.DrawLine(icon, 32, 22 + offset, 32, 31 + offset); g.DrawLine(icon, 40, 22 + offset, 40, 31 + offset); }
                else if (stateKind == "attention" || stateKind == "error")
                {
                    g.DrawArc(icon, 30, 19 + offset, 12, 14, 180, 180);
                    g.DrawLine(icon, 30, 26 + offset, 28, 32 + offset); g.DrawLine(icon, 42, 26 + offset, 44, 32 + offset);
                    g.DrawLine(icon, 28, 32 + offset, 44, 32 + offset); g.DrawArc(icon, 34, 32 + offset, 4, 4, 0, 180);
                }
                else
                {
                    using (SolidBrush dot = new SolidBrush(accent)) g.FillEllipse(dot, 32, 22 + offset, 8, 8);
                    if (stateKind == "running") g.DrawArc(icon, 27, 17 + offset, 18, 18, -60, 95);
                }
            }
            using (Font label = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush ink = new SolidBrush(Enabled ? Color.FromArgb(239, 245, 251) : Color.FromArgb(150, 162, 177)))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center; format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter; format.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(statusText, label, ink, new RectangleF(7, 38 + offset, 58, 21), format);
            }
            g.Restore(saved);
        }
    }
}
