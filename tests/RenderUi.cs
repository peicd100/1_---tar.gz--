using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
class RenderUi {
    [STAThread] static int Main(string[] args) {
        var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        assembly.GetType("Peicd.TarGz.Brand").GetMethod("Init", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        var type = assembly.GetType(args[1]);
        object[] parameters = args[1].EndsWith("ArchiveWindow") ? new object[] { new string[0] } : new object[0];
        using (var form = (Form)Activator.CreateInstance(type, BindingFlags.NonPublic | BindingFlags.Instance, null, parameters, null)) {
            form.ShowInTaskbar = false; form.Opacity = 0;
            form.Show(); Application.DoEvents(); form.PerformLayout();
            if (args.Length > 3) {
                var bar = type.GetField("bar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                bar.GetType().GetProperty("Percent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bar, int.Parse(args[3]), null);
                if (args[3] == "100") {
                    var status = (Label)type.GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    status.Text = "完成，壓縮檔可讀性檢查通過。";
                }
            }
            if (args[1].EndsWith("SetupWindow")) {
                // Let the read-only package query finish before rendering.
                var until = DateTime.UtcNow.AddSeconds(12);
                while (DateTime.UtcNow < until) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }
            }
            using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) {
                var panel = form.Controls[0]; panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
                bitmap.Save(args[2], System.Drawing.Imaging.ImageFormat.Png);
            }
            form.Close();
        }
        return 0;
    }
}
