using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Peicd.TarGz {
    internal sealed class DarkButton : Button {
        protected override void OnPaint(PaintEventArgs e) {
            if (Enabled) { base.OnPaint(e); return; }
            e.Graphics.Clear(Brand.Surface);
            using (var pen = new Pen(Color.FromArgb(60, 60, 58))) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Color.FromArgb(125, 123, 117), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
    internal sealed class ProgressStrip : Control {
        int percent;
        internal int Percent {
            get { return percent; }
            set { percent = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }
        internal ProgressStrip() {
            DoubleBuffered = true; Height = 30; BackColor = Brand.Background;
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            TextRenderer.DrawText(e.Graphics, percent + "%", Font, new Rectangle(0, 0, Width, Height - 8), Brand.Ink,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            using (var track = new SolidBrush(Brand.Surface)) e.Graphics.FillRectangle(track, 0, Height - 6, Width, 6);
            using (var brush = new SolidBrush(Brand.Ink)) {
                e.Graphics.FillRectangle(brush, 0, Height - 6, (int)(Width * percent / 100.0), 6);
            }
        }
    }
    internal static class Brand {
        internal const string Title = "壓縮成 tar.gz";
        internal static string Version { get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(3); } }
        internal const string Package = "PEICD.TarGz";
        internal static readonly Color Background = Color.FromArgb(20, 20, 20);
        internal static readonly Color Surface = Color.FromArgb(32, 32, 32);
        internal static readonly Color Ink = Color.FromArgb(242, 235, 218);
        static PrivateFontCollection fonts = new PrivateFontCollection();
        static IntPtr fontMemory;
        [DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(IntPtr data, uint size, IntPtr reserved, ref uint count);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        internal static void Init() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("font.ttf")) {
                if (stream == null) throw new InvalidOperationException("缺少清松手寫體 9 字體資源。");
                byte[] bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length) read += stream.Read(bytes, read, bytes.Length - read);
                fontMemory = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, fontMemory, bytes.Length);
                fonts.AddMemoryFont(fontMemory, bytes.Length);
                uint count = 0;
                AddFontMemResourceEx(fontMemory, (uint)bytes.Length, IntPtr.Zero, ref count);
            }
        }
        internal static Font Font(float size) { return new Font(fonts.Families[0], size, FontStyle.Regular); }
        internal static Icon Icon() {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico")) {
                return new Icon(s);
            }
        }
        internal static Form Window(string title) {
            Form f = new Form { Text = title, BackColor = Background, ForeColor = Ink,
                Font = Font(13), ClientSize = new Size(700, 500), MinimumSize = new Size(650, 470),
                StartPosition = FormStartPosition.CenterScreen, Icon = Icon(), AutoScaleMode = AutoScaleMode.Dpi };
            DarkTitle(f);
            return f;
        }
        internal static void DarkTitle(Form f) {
            f.HandleCreated += delegate { try { int dark = 1; DwmSetWindowAttribute(f.Handle, 20, ref dark, 4); } catch { } };
        }
        internal static Label Label(string text, float size) {
            return new Label { Text = text, Font = Font(size), ForeColor = Ink, AutoSize = true,
                MaximumSize = new Size(610, 0), UseMnemonic = false, Margin = new Padding(0, 0, 0, 12) };
        }
        internal static Button Button(string text) {
            Button b = new DarkButton { Text = text, AutoSize = true, MinimumSize = new Size(130, 44),
                BackColor = Surface, ForeColor = Ink, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                Padding = new Padding(12, 3, 12, 3), Margin = new Padding(0, 0, 12, 0), UseMnemonic = false };
            b.FlatAppearance.BorderColor = Color.FromArgb(84, 81, 74);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(49, 49, 47);
            return b;
        }
        internal static TableLayoutPanel Layout(Form f) {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(32), ColumnCount = 1, RowCount = 6 };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            f.Controls.Add(p);
            return p;
        }
    }
}
