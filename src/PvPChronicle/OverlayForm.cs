using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PvPChronicle
{
    public sealed class OverlayForm : Form
    {
        private PluginSettings settings;
        private List<HistoryEntry> entries = new List<HistoryEntry>();
        private bool contentVisible, restoring;
        private int scroll;
        private readonly Color background = Color.FromArgb(24, 27, 30);
        public event EventHandler BoundsChangedByUser;
        public OverlayForm(PluginSettings value)
        {
            settings = value; AutoScaleMode = AutoScaleMode.None; BackColor = background;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            TopMost = true; DoubleBuffered = true; MinimumSize = new Size(360, 160); MaximumSize = new Size(1400, 900);
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            Move += BoundsChanged; ResizeEnd += BoundsChanged;
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x80 | 0x08000000 | 0x80000; return p; } }
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr window, int index, int value);
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84)
            {
                if (!contentVisible || settings.Locked) { m.Result = (IntPtr)(-1); return; }
                var point = PointToClient(new Point(unchecked((short)((long)m.LParam & 0xffff)), unchecked((short)(((long)m.LParam >> 16) & 0xffff))));
                m.Result = (IntPtr)(point.X >= Width - 18 && point.Y >= Height - 18 ? 17 : 2); return;
            }
            base.WndProc(ref m);
        }
        public void ApplySettings(PluginSettings value, bool restore = false)
        {
            settings = value; settings.Normalize();
            if (restore)
            {
                restoring = true;
                var r = new Rectangle(settings.Left, settings.Top, settings.Width, settings.Height);
                if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(r))) r.Location = new Point(100, 100);
                Bounds = r; restoring = false;
            }
            RefreshVisibility(); Invalidate();
        }
        public void SetEntries(IEnumerable<HistoryEntry> value)
        {
            entries = value.Take(30).ToList(); scroll = 0; RefreshVisibility(); Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); if (settings.Locked) return; scroll = Math.Max(0, Math.Min(Math.Min(settings.MaximumLines, entries.Count) - 1, scroll - Math.Sign(e.Delta))); Invalidate(); }
        private void RefreshVisibility()
        {
            contentVisible = settings.OverlayEnabled && (settings.Preview || entries.Count > 0);
            if (!Visible) { Opacity = 0; Show(); }
            var style = GetWindowLong(Handle, -20);
            var wanted = settings.Locked || !contentVisible ? style | 0x20 : style & ~0x20;
            if (style != wanted) SetWindowLong(Handle, -20, wanted);
            var opacity = contentVisible ? Math.Min(.99, settings.OpacityPercent / 100d) : 0;
            if (Math.Abs(Opacity - opacity) > .001) Opacity = opacity;
        }
        private void BoundsChanged(object sender, EventArgs e)
        {
            if (restoring || settings.Locked) return;
            settings.Left = Left; settings.Top = Top; settings.Width = Width; settings.Height = Height;
            BoundsChangedByUser?.Invoke(this, EventArgs.Empty);
        }
        internal bool ContentVisibleForTest => contentVisible;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            using (var title = new Font(Localization.FontFamily(settings.Language), 11, FontStyle.Bold))
            using (var text = new Font(Localization.FontFamily(settings.Language), settings.FontSize))
            using (var small = new Font(Localization.FontFamily(settings.Language), 9))
            using (var gold = new Pen(Color.FromArgb(202, 165, 87), 2))
            {
                Draw(g, "PvP Chronicle", title, new Rectangle(12, 5, Width - 24, 26), Color.White);
                g.DrawLine(gold, 12, 34, Width - 12, 34);
                var visible = entries.Take(settings.MaximumLines).ToList();
                if (settings.Preview && visible.Count == 0)
                    visible.Add(new HistoryEntry { Timestamp = DateTimeOffset.Now, Kind = "EstimatedAssist", TargetName = Localization.Get(settings.Language, "PreviewTarget"), ActionName = Localization.Get(settings.Language, "PreviewAction") });
                int y = 42, rowHeight = text.Height + small.Height + 12;
                foreach (var entry in visible.Skip(scroll))
                {
                    if (y + rowHeight > Height - 24) break;
                    var color = entry.Kind == "Kill" ? Color.FromArgb(117, 221, 170) : Color.FromArgb(242, 196, 105);
                    Draw(g, entry.Timestamp.ToLocalTime().ToString("HH:mm:ss") + "  " + Localization.Get(settings.Language, entry.Kind), small, new Rectangle(12, y, Width - 24, small.Height + 2), color);
                    var split = (int)((Width - 24) * .44);
                    Draw(g, entry.TargetName, text, new Rectangle(12, y + small.Height + 2, split - 8, text.Height + 2), Color.White);
                    Draw(g, string.IsNullOrWhiteSpace(entry.ActionName) ? Localization.Get(settings.Language, "Unknown") : entry.ActionName, text,
                        new Rectangle(12 + split, y + small.Height + 2, Width - 24 - split, text.Height + 2), Color.FromArgb(189, 201, 211));
                    y += rowHeight;
                }
                var shown = Math.Max(0, (y - 42) / rowHeight);
                Draw(g, (visible.Count == 0 ? "0" : (scroll + 1) + "-" + (scroll + shown)) + " / " + visible.Count, small, new Rectangle(12, Height - 22, Width - 40, 20), Color.FromArgb(174, 185, 196));
                if (!settings.Locked) { g.DrawLine(Pens.Gray, Width - 16, Height - 5, Width - 5, Height - 16); g.DrawLine(Pens.Gray, Width - 10, Height - 5, Width - 5, Height - 10); }
            }
        }
        private static void Draw(Graphics g, string value, Font font, Rectangle bounds, Color color) =>
            TextRenderer.DrawText(g, value ?? "", font, bounds, color, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}
