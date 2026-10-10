using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Peicd.TarGz {
    static class App {
        [STAThread] static int Main(string[] args) {
            try {
                if (args.Length > 0 && args[0] == "--compress") return Cli(args);
                Brand.Init();
                string[] paths = args;
                if (args.Length == 2 && args[0] == "--manifest") {
                    string manifest = Path.GetFullPath(args[1]);
                    if (!string.Equals(Path.GetDirectoryName(manifest).TrimEnd('\\'), Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                        || !Path.GetFileName(manifest).StartsWith("peicd-targz-") || !manifest.EndsWith(".txt"))
                        throw new IOException("無效的選取清單位置。");
                    paths = File.ReadAllLines(manifest, System.Text.Encoding.Unicode);
                    File.Delete(manifest);
                }
                Application.Run(new ArchiveWindow(paths)); return 0;
            } catch (Exception e) { MessageBox.Show(e.Message, Brand.Title, MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
        static int Cli(string[] args) {
            int split = Array.IndexOf(args, "--");
            if (split < 0) return 2;
            string report = null; int cancel = 0;
            for (int i = 1; i < split; i++) {
                if (args[i] == "--report" && i + 1 < split) report = args[++i];
                else if (args[i] == "--cancel-after-ms" && i + 1 < split) cancel = int.Parse(args[++i]);
                else return 2;
            }
            try {
                using (var cts = new CancellationTokenSource()) {
                    if (cancel > 0) cts.CancelAfter(cancel);
                    string output = Archive.Create(args.Skip(split + 1).ToArray(), new Progress<ArchiveProgress>(), cts.Token).GetAwaiter().GetResult();
                    if (report != null) File.WriteAllText(report, output, new System.Text.UTF8Encoding(false));
                    return 0;
                }
            } catch (Exception e) {
                if (report != null) File.WriteAllText(report, e.GetType().Name + ": " + e.Message, new System.Text.UTF8Encoding(false));
                return e is OperationCanceledException ? 3 : 1;
            }
        }
    }
    sealed class ArchiveWindow : Form {
        CancellationTokenSource cts; bool running; string output;
        Label status, elapsed; TextBox details; Button action, open; ProgressStrip bar;
        sealed class LatestProgress : IProgress<ArchiveProgress> {
            internal ArchiveProgress Value;
            public void Report(ArchiveProgress value) { System.Threading.Interlocked.Exchange(ref Value, value); }
        }
        readonly LatestProgress progress = new LatestProgress();
        readonly Stopwatch watch = new Stopwatch(); string[] paths;
        internal ArchiveWindow(string[] selection) {
            paths = selection;
            Form theme = Brand.Window(Brand.Title);
            Text = theme.Text; BackColor = theme.BackColor; ForeColor = theme.ForeColor; Font = theme.Font;
            ClientSize = theme.ClientSize; MinimumSize = theme.MinimumSize; Icon = theme.Icon;
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            theme.Dispose();
            Brand.DarkTitle(this);
            var layout = Brand.Layout(this);
            layout.Controls.Add(Brand.Label(Brand.Title, 27));
            status = Brand.Label(paths.Length == 0 ? "選取檔案，開始封存。" : "正在準備…", 16); layout.Controls.Add(status);
            bar = new ProgressStrip { Dock = DockStyle.Top, Height = 30, Font = Brand.Font(12), Margin = new Padding(0, 8, 0, 20) }; layout.Controls.Add(bar);
            details = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
                BackColor = Brand.Surface, ForeColor = Brand.Ink, BorderStyle = BorderStyle.None, Font = Brand.Font(12),
                HideSelection = true, Text = paths.Length == 0 ? "也可以從檔案總管右鍵選單啟動。\r\n\r\n選取同一位置的多個項目，會合併成一個 tar.gz。" : string.Join("\r\n", paths) };
            details.Select(0, 0);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(details);
            elapsed = Brand.Label("v" + Brand.Version + "  ·  清松手寫體 9 文青", 11); elapsed.Margin = new Padding(0, 16, 0, 16); layout.Controls.Add(elapsed);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            action = Brand.Button(paths.Length == 0 ? "選擇檔案" : "取消壓縮");
            open = Brand.Button("開啟所在資料夾"); open.Visible = false;
            open.Click += delegate { Process.Start("explorer.exe", "/select," + Archive.Quote(output)); };
            action.Click += async delegate {
                if (running) { action.Enabled = false; status.Text = "正在取消，清理暫存檔…"; cts.Cancel(); }
                else if (paths.Length == 0) {
                    using (var dialog = new OpenFileDialog { Multiselect = true, Title = "選取要壓縮的檔案" })
                        if (dialog.ShowDialog(this) == DialogResult.OK) { paths = dialog.FileNames; await Start(); }
                } else Close();
            };
            buttons.Controls.Add(action); buttons.Controls.Add(open); layout.Controls.Add(buttons);
            var timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += delegate { if (running) { elapsed.Text = "已經過 " + watch.Elapsed.ToString(@"hh\:mm\:ss");
                ArchiveProgress latest = System.Threading.Interlocked.CompareExchange(ref progress.Value, null, null);
                if (!cts.IsCancellationRequested && latest != null) { status.Text = latest.Status; bar.Percent = latest.Percent; } } };
            timer.Start(); FormClosed += delegate { timer.Dispose(); if (cts != null) cts.Dispose(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (running) { e.Cancel = true; cts.Cancel(); status.Text = "正在取消，清理暫存檔…"; action.Enabled = false; }
            };
            Shown += async delegate { ActiveControl = action; details.Select(0, 0); if (paths.Length > 0) await Start(); };
        }
        async Task Start() {
            running = true; cts = new CancellationTokenSource(); watch.Restart(); action.Text = "取消壓縮";
            bar.Percent = 0; progress.Value = null; details.Text = string.Join("\r\n", paths);
            try {
                output = await Archive.Create(paths, progress, cts.Token);
                status.Text = "完成，壓縮檔可讀性檢查通過。"; details.Text = output + "\r\n\r\n大小：" + (new FileInfo(output).Length / 1048576.0).ToString("N2") + " MiB";
                open.Visible = true; bar.Percent = 100;
            } catch (OperationCanceledException) { status.Text = "已取消。"; details.Text = "原始檔案已保留，本次暫存檔已清除。"; }
            catch (Exception e) { status.Text = "壓縮未完成。"; details.Text = e.Message; }
            finally { watch.Stop(); running = false; bar.Invalidate(); action.Text = "關閉"; action.Enabled = true;
                elapsed.Text = "耗時 " + watch.Elapsed.ToString(@"hh\:mm\:ss") + "  ·  v" + Brand.Version; }
        }
    }
}
