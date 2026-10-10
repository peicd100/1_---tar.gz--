using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Peicd.TarGz {
    static class Setup {
        internal static byte[] Resource(string name) {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (var memory = new MemoryStream()) { stream.CopyTo(memory); return memory.ToArray(); }
        }
        internal static X509Certificate2 Certificate() { return new X509Certificate2(Resource("signer.cer")); }
        [STAThread] static int Main(string[] args) {
            try {
                if (args.Length == 1 && (args[0] == "--trust" || args[0] == "--untrust")) {
                    // Always use embedded public certificate; never accept an external trust target.
                    using (var cert = Certificate())
                    using (var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine)) {
                        store.Open(OpenFlags.ReadWrite);
                        if (args[0] == "--trust") store.Add(cert);
                        else foreach (var match in store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false)) store.Remove(match);
                    }
                    return 0;
                }
                if (args.Length == 2 && args[0] == "--extract") {
                    // Offline artifact inspection only: no registration, trust changes, or installation.
                    Directory.CreateDirectory(args[1]);
                    File.WriteAllBytes(Path.Combine(args[1], "TarGz.msix"), CheckedPayload());
                    File.WriteAllBytes(Path.Combine(args[1], "signer.cer"), Resource("signer.cer"));
                    return 0;
                }
                Brand.Init(); Application.Run(new SetupWindow()); return 0;
            } catch (Exception e) { MessageBox.Show(e.Message, "TAR.GZ 安裝程式", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
        internal static byte[] CheckedPayload() {
            byte[] bytes = Resource("payload.msix");
            using (var hash = SHA256.Create()) {
                string digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (digest != PayloadHash.Value) throw new IOException("安裝套件 SHA-256 檢查失敗。");
            }
            return bytes;
        }
        internal static bool Trusted() {
            using (var cert = Certificate())
            using (var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine)) {
                store.Open(OpenFlags.ReadOnly);
                return store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).Count > 0;
            }
        }
        internal static async Task Trust(bool install) {
            var info = new ProcessStartInfo(Application.ExecutablePath, install ? "--trust" : "--untrust") { UseShellExecute = true, Verb = "runas" };
            using (var process = Process.Start(info)) {
                await Task.Run(() => process.WaitForExit());
                if (process.ExitCode != 0) throw new IOException("憑證信任變更未完成。請確認系統管理員授權。");
            }
        }
        internal static async Task<string> PackageAction(string action, string payload) {
            string directory = Path.Combine(Path.GetTempPath(), "peicd-targz-setup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string script = Path.Combine(directory, "package.ps1");
            try {
                File.WriteAllText(script, @"param([string]$Action,[string]$Payload)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
try {
 $package = Get-AppxPackage -Name 'PEICD.TarGz' | Sort-Object Version -Descending | Select-Object -First 1
 switch ($Action) {
  'query' { if ($package) { Write-Output ($package.Version.ToString() + '|' + $package.PackageFullName) } }
  'install' { Add-AppxPackage -Path $Payload -ErrorAction Stop; Write-Output 'installed' }
  'repair' { if (-not $package) { throw 'Package is not installed' }; Add-AppxPackage -Register (Join-Path $package.InstallLocation 'AppxManifest.xml') -DisableDevelopmentMode -ErrorAction Stop; Write-Output 'registered' }
  'remove' { if ($package) { Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop }; Write-Output 'removed' }
  default { throw 'Unknown package action' }
 }
 exit 0
} catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }
", new System.Text.UTF8Encoding(true));
                string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                var info = new ProcessStartInfo(ps, "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Archive.Quote(script) + " -Action " + Archive.Quote(action) + " -Payload " + Archive.Quote(payload ?? "")) {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
                using (var process = Process.Start(info)) {
                    Task<string> stdout = process.StandardOutput.ReadToEndAsync(); Task<string> stderr = process.StandardError.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());
                    string output = await stdout; string error = await stderr;
                    if (process.ExitCode != 0) throw new IOException(error.Trim());
                    return output.Trim();
                }
            } finally { if (File.Exists(script)) File.Delete(script); Directory.Delete(directory); }
        }
    }
    sealed class SetupWindow : Form {
        Label state; TextBox details; Button install, remove, certificate; CheckBox consent; bool busy;
        string installed = "";
        internal SetupWindow() {
            Form theme = Brand.Window("TAR.GZ · 安裝與管理");
            Text = theme.Text; BackColor = theme.BackColor; ForeColor = theme.ForeColor; Font = theme.Font;
            ClientSize = new System.Drawing.Size(740, 570); MinimumSize = new System.Drawing.Size(700, 550);
            Icon = theme.Icon; StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi; theme.Dispose();
            Brand.DarkTitle(this);
            var layout = Brand.Layout(this);
            layout.Controls.Add(Brand.Label(Brand.Title, 27));
            state = Brand.Label("正在檢查安裝狀態…", 16); layout.Controls.Add(state);
            consent = new CheckBox { Text = "允許管理此工具的簽章憑證（Windows 會要求系統管理員授權）", AutoSize = true,
                ForeColor = Brand.Ink, MaximumSize = new System.Drawing.Size(650, 0), Margin = new Padding(0, 4, 0, 16) };
            layout.Controls.Add(consent);
            details = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
                BackColor = Brand.Surface, ForeColor = Brand.Ink, BorderStyle = BorderStyle.None, Font = Brand.Font(12) };
            layout.Controls.Add(details);
            layout.Controls.Add(Brand.Label("Windows 11 x64  ·  v" + Brand.Version + "  ·  離線安裝", 11));
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            install = Brand.Button("安裝"); remove = Brand.Button("解除安裝"); certificate = Brand.Button("憑證資訊");
            install.Enabled = remove.Enabled = false;
            install.Click += async delegate { await Apply(false); };
            remove.Click += async delegate { await Apply(true); };
            certificate.Click += delegate {
                using (var cert = Setup.Certificate()) details.Text = "簽章對象：" + cert.Subject + "\r\nSHA-1 指紋：" + cert.Thumbprint +
                    "\r\n有效期限：" + cert.NotAfter.ToString("yyyy-MM-dd") + "\r\n\r\n這是本工具的本機自簽憑證，僅加入 TrustedPeople，不加入根憑證授權單位。\r\n首次安裝須勾選授權。解除安裝時勾選授權，會一併移除此憑證信任。";
            };
            buttons.Controls.Add(install); buttons.Controls.Add(remove); buttons.Controls.Add(certificate); layout.Controls.Add(buttons);
            for (int i = 0; i < 6; i++) layout.RowStyles.Add(new RowStyle(i == 3 ? SizeType.Percent : SizeType.AutoSize, i == 3 ? 100 : 0));
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) e.Cancel = true; };
            Shown += async delegate { try { await RefreshState(); } catch (Exception e) { state.Text = "無法讀取安裝狀態。"; details.Text = e.Message; } };
        }
        async Task RefreshState() {
            installed = await Setup.PackageAction("query", "");
            state.Text = installed.Length == 0 ? "尚未安裝。" : "已安裝 v" + installed.Split('|')[0];
            install.Text = installed.Length == 0 ? "安裝" : "更新／修復";
            if (installed.Length > 0 && new Version(installed.Split('|')[0]) == new Version(Brand.Version + ".0")) install.Text = "修復註冊";
            install.Enabled = true; remove.Enabled = installed.Length > 0; certificate.Enabled = true;
            if (installed.Length > 0 && new Version(installed.Split('|')[0]) > new Version(Brand.Version + ".0")) {
                install.Enabled = false; state.Text = "已安裝較新版本，請使用新版安裝程式。";
            }
            details.Text = "右鍵選單：壓縮成 tar.gz\r\n黑色簡約介面 · 清松手寫體 9 文青\r\n\r\n首次安裝需授權本機簽章憑證。更新時請先關閉本工具並等壓縮工作完成。\r\n\r\n解除安裝會移除程式和右鍵註冊，保留您產生的壓縮檔。\r\n若從 Windows 設定解除安裝，憑證信任仍會保留；可再執行本安裝程式勾選授權後移除。";
            if (installed.Length == 0 && Setup.Trusted()) { remove.Enabled = true; remove.Text = "清除憑證信任"; }
            else remove.Text = "解除安裝";
        }
        async Task Apply(bool uninstall) {
            busy = true; install.Enabled = remove.Enabled = certificate.Enabled = consent.Enabled = false;
            string directory = null; bool failed = false;
            try {
                if (!uninstall && !Setup.Trusted() && !consent.Checked) {
                    state.Text = "請先勾選憑證管理授權，再開始安裝。"; return;
                }
                if (uninstall && installed.Length == 0 && !consent.Checked) {
                    state.Text = "請勾選憑證管理授權，才能清除信任。"; return;
                }
                if (uninstall) {
                    state.Text = "正在解除安裝…";
                    await Setup.PackageAction("remove", "");
                    if (consent.Checked && Setup.Trusted()) await Setup.Trust(false);
                    await RefreshState(); state.Text = "解除安裝完成。";
                    details.Text = "程式與右鍵註冊已移除。您的壓縮檔已保留。\r\n" + (Setup.Trusted() ? "簽章憑證信任仍保留；如需清除，勾選授權後按清除憑證信任。" : "本工具的憑證信任已清除。") + "\r\n\r\n若選單仍顯示舊項目，請重新登入 Windows 讓檔案總管重新載入。";
                } else {
                    state.Text = "正在檢查套件…";
                    directory = Path.Combine(Path.GetTempPath(), "peicd-targz-payload-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
                    string payload = Path.Combine(directory, "TarGz.msix"); File.WriteAllBytes(payload, Setup.CheckedPayload());
                    if (!Setup.Trusted()) { state.Text = "等待憑證管理授權…"; await Setup.Trust(true); }
                    state.Text = "正在安裝／更新…";
                    bool same = installed.Length > 0 && new Version(installed.Split('|')[0]) == new Version(Brand.Version + ".0");
                    await Setup.PackageAction(same ? "repair" : "install", payload);
                    await RefreshState(); state.Text = "安裝完成。";
                    details.Text = "在檔案總管選取檔案或資料夾，按右鍵 → 壓縮成 tar.gz。\r\n\r\n若選單尚未更新，請重新登入 Windows 讓檔案總管重新載入。\r\n\r\n下次更新：執行新版 Setup.exe。\r\n解除安裝：執行本安裝程式，或前往 Windows「已安裝的應用程式」。";
                }
            } catch (Exception e) {
                state.Text = "作業未完成。";
                details.Text = e.Message + "\r\n\r\n請先關閉壓縮程式；若為公司管理電腦，可能受應用程式安裝政策限制。\r\n憑證管理與安裝是兩個步驟：若憑證已加入而安裝失敗，可解除安裝／清除憑證信任。";
                failed = true;
            } finally {
                if (directory != null) { string file = Path.Combine(directory, "TarGz.msix"); if (File.Exists(file)) File.Delete(file); Directory.Delete(directory); }
                busy = false; consent.Enabled = certificate.Enabled = true;
                install.Enabled = installed.Length == 0 || new Version(installed.Split('|')[0]) <= new Version(Brand.Version + ".0");
                remove.Enabled = installed.Length > 0 || Setup.Trusted();
            }
            if (failed) {
                try { installed = await Setup.PackageAction("query", ""); } catch { }
                remove.Enabled = installed.Length > 0 || Setup.Trusted();
                remove.Text = installed.Length == 0 ? "清除憑證信任" : "解除安裝";
            }
        }
    }
}
