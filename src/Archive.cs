using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Peicd.TarGz {
    internal sealed class ArchiveProgress {
        internal readonly string Status;
        internal readonly int Percent;
        internal ArchiveProgress(string status, int percent) { Status = status; Percent = percent; }
    }
    // POSIX ustar + UTF-8 PAX paths. No process/argv/locale dependency.
    internal static class Archive {
        sealed class WorkProgress {
            readonly IProgress<ArchiveProgress> target; readonly long total;
            long done; string status;
            internal WorkProgress(IProgress<ArchiveProgress> target, long total) { this.target = target; this.total = total; }
            internal void Status(string value) { status = value; Report(); }
            internal void Advance(long amount) { done += amount; Report(); }
            void Report() { target.Report(new ArchiveProgress(status, (int)Math.Min(90, 90.0 * done / Math.Max(1, total)))); }
        }
        static long CountWork(string path, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            FileAttributes attr = File.GetAttributes(path); RejectLinks(path, attr);
            if ((attr & FileAttributes.Directory) == 0) return checked(new FileInfo(path).Length + 1);
            long total = 1;
            foreach (string child in Directory.EnumerateFileSystemEntries(path)) total = checked(total + CountWork(child, token));
            return total;
        }
        internal static string Quote(string value) {
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { b.Append('\\', slashes * 2 + 1).Append('"'); slashes = 0; continue; }
                b.Append('\\', slashes).Append(c); slashes = 0;
            }
            return b.Append('\\', slashes * 2).Append('"').ToString();
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle file, uint code, IntPtr input, int inputSize, byte[] output, int outputSize, out int returned, IntPtr overlapped);
        static void RejectLinks(string path, FileAttributes attr) {
            if ((attr & FileAttributes.ReparsePoint) == 0) return;
            using (var f = CreateFile(path, 0, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero)) {
                byte[] buffer = new byte[16384]; int n;
                if (f.IsInvalid || !DeviceIoControl(f, 0x000900a8, IntPtr.Zero, 0, buffer, buffer.Length, out n, IntPtr.Zero))
                    throw new IOException("無法確認重新解析點：" + path);
                uint tag = BitConverter.ToUInt32(buffer, 0);
                if (tag == 0xa000000c || tag == 0xa0000003) throw new IOException("此版本不支援符號連結或 junction，已停止：" + path);
            }
        }
        static byte[] Pax(string key, string value) {
            string body = key + "=" + value + "\n"; int count = Encoding.UTF8.GetByteCount(body), length = count + 3;
            while (true) { int actual = count + length.ToString().Length + 1; if (actual == length) break; length = actual; }
            return Encoding.UTF8.GetBytes(length.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + body);
        }
        static void Text(byte[] h, int offset, string value) { byte[] data = Encoding.ASCII.GetBytes(value); Buffer.BlockCopy(data, 0, h, offset, data.Length); }
        static void Octal(byte[] h, int offset, int width, long value) {
            string text = Convert.ToString(value, 8); if (text.Length >= width) throw new IOException("TAR 欄位超過範圍。");
            Text(h, offset, text.PadLeft(width - 1, '0') + "\0");
        }
        static long Unix(DateTime time) { return Math.Max(0, (long)(time.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds); }
        static void Header(Stream stream, string name, long size, char type, long modified) {
            byte[] h = new byte[512]; Text(h, 0, name);
            Octal(h, 100, 8, type == '5' ? 493 : 420); Octal(h, 108, 8, 0); Octal(h, 116, 8, 0);
            Octal(h, 124, 12, size); Octal(h, 136, 12, modified);
            for (int i = 148; i < 156; i++) h[i] = 32;
            h[156] = (byte)type; Text(h, 257, "ustar\0"); Text(h, 263, "00");
            Text(h, 148, Convert.ToString(h.Sum(b => (long)b), 8).PadLeft(6, '0') + "\0 "); stream.Write(h, 0, 512);
        }
        static void Pad(Stream stream, long size) { int count = (int)((512 - size % 512) % 512); if (count > 0) stream.Write(new byte[count], 0, count); }
        static void Add(Stream stream, string source, string entry, ref int index, WorkProgress progress, CancellationToken token) {
            token.ThrowIfCancellationRequested(); FileAttributes attr = File.GetAttributes(source); RejectLinks(source, attr);
            bool directory = (attr & FileAttributes.Directory) != 0; if (directory) entry = entry.TrimEnd('/') + "/";
            progress.Status("正在壓縮：" + entry);
            long size = directory ? 0 : new FileInfo(source).Length; DateTime modified = File.GetLastWriteTimeUtc(source);
            byte[] path = Pax("path", entry), extra = size > 8589934591L ? Pax("size", size.ToString(System.Globalization.CultureInfo.InvariantCulture)) : new byte[0];
            index++;
            Header(stream, "PaxHeaders/" + index, path.Length + extra.Length, 'x', Unix(modified));
            stream.Write(path, 0, path.Length); stream.Write(extra, 0, extra.Length); Pad(stream, path.Length + extra.Length);
            Header(stream, "entry-" + index, size > 8589934591L ? 0 : size, directory ? '5' : '0', Unix(modified));
            progress.Advance(1); // Count empty files/directories too.
            if (directory) {
                foreach (string child in Directory.EnumerateFileSystemEntries(source)) Add(stream, child, entry + Path.GetFileName(child), ref index, progress, token);
            } else {
                using (var file = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.SequentialScan)) {
                    if (file.Length != size) throw new IOException("檔案在壓縮期間變更：" + source);
                    byte[] buffer = new byte[131072]; long remaining = size;
                    while (remaining > 0) {
                        token.ThrowIfCancellationRequested(); int count = file.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (count == 0) throw new EndOfStreamException("檔案在壓縮期間縮短：" + source);
                        stream.Write(buffer, 0, count); remaining -= count;
                        progress.Advance(count);
                    }
                    if (file.Length != size || File.GetLastWriteTimeUtc(source) != modified) throw new IOException("檔案在壓縮期間變更：" + source);
                }
                Pad(stream, size);
            }
        }
        static void ReadExact(Stream stream, byte[] buffer, int count, CancellationToken token) {
            int read = 0; while (read < count) { token.ThrowIfCancellationRequested(); int n = stream.Read(buffer, read, count - read); if (n == 0) throw new IOException("壓縮檔內容不完整。"); read += n; }
        }
        internal static void Validate(string path, CancellationToken token, Action<long, long> onProgress = null) {
            using (var file = File.OpenRead(path)) using (var gzip = new GZipStream(file, CompressionMode.Decompress)) {
                byte[] h = new byte[512], buffer = new byte[131072]; long paxSize = -1;
                while (true) {
                    if (onProgress != null) onProgress(file.Position, file.Length);
                    ReadExact(gzip, h, 512, token);
                    if (h.All(b => b == 0)) {
                        ReadExact(gzip, h, 512, token); if (h.Any(b => b != 0)) throw new IOException("TAR 結尾不完整。");
                        while (true) { token.ThrowIfCancellationRequested(); int n = gzip.Read(buffer, 0, buffer.Length); if (n == 0) break; if (buffer.Take(n).Any(b => b != 0)) throw new IOException("TAR 結尾有額外資料。"); }
                        if (onProgress != null) onProgress(file.Length, file.Length);
                        return;
                    }
                    long checksum = Convert.ToInt64(Encoding.ASCII.GetString(h, 148, 8).Trim('\0', ' '), 8), sum = 0;
                    for (int i = 0; i < 512; i++) sum += i >= 148 && i < 156 ? 32 : h[i];
                    if (sum != checksum) throw new IOException("TAR 標頭校驗失敗。");
                    long size = Convert.ToInt64(Encoding.ASCII.GetString(h, 124, 12).Trim('\0', ' '), 8); bool pax = h[156] == (byte)'x';
                    if (!pax && paxSize >= 0) { size = paxSize; paxSize = -1; }
                    if (pax) {
                        if (size > 1048576) throw new IOException("PAX 標頭過大。");
                        byte[] data = new byte[(int)size]; ReadExact(gzip, data, data.Length, token);
                        foreach (string line in Encoding.UTF8.GetString(data).Split('\n')) {
                            int space = line.IndexOf(' ');
                            if (space >= 0 && line.Substring(space + 1).StartsWith("size=")) paxSize = long.Parse(line.Substring(space + 6), System.Globalization.CultureInfo.InvariantCulture);
                        }
                        ReadExact(gzip, buffer, (int)((512 - size % 512) % 512), token);
                    } else {
                        long remaining = size + (512 - size % 512) % 512;
                        while (remaining > 0) { int n = (int)Math.Min(buffer.Length, remaining); ReadExact(gzip, buffer, n, token); remaining -= n;
                            if (onProgress != null) onProgress(file.Position, file.Length); }
                    }
                }
            }
        }
        internal static Task<string> Create(string[] selection, IProgress<ArchiveProgress> progress, CancellationToken token) {
            return Task.Run(() => CreateInternal(selection, progress, token), token);
        }
        static string CreateInternal(string[] selection, IProgress<ArchiveProgress> progress, CancellationToken token) {
            progress.Report(new ArchiveProgress("正在計算檔案大小…", 0));
            string[] paths = selection.Select(p => Path.GetFullPath(p).TrimEnd('\\')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (paths.Length == 0) throw new IOException("請選取檔案或資料夾。");
            foreach (string path in paths) {
                if (path.Equals(Path.GetPathRoot(path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) throw new IOException("不支援直接壓縮磁碟根目錄。");
                if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("找不到選取項目。", path);
            }
            string[] parents = paths.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (parents.Length != 1) throw new IOException("請選取同一個資料夾內的項目，再合併壓縮。");
            long total = 0; foreach (string path in paths) total = checked(total + CountWork(path, token));
            var work = new WorkProgress(progress, total);
            string parent = parents[0], stem = paths.Length == 1 ? Path.GetFileName(paths[0]) : "選取項目_" + paths.Length + "個_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string partial = Path.Combine(parent, ".peicd-targz-" + Guid.NewGuid().ToString("N") + ".partial");
            try {
                using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.SequentialScan))
                using (var gzip = new GZipStream(file, CompressionLevel.Optimal)) {
                    int index = 0; foreach (string source in paths) Add(gzip, source, "./" + Path.GetFileName(source), ref index, work, token);
                    token.ThrowIfCancellationRequested(); gzip.Write(new byte[1024], 0, 1024);
                }
                progress.Report(new ArchiveProgress("壓縮完成，正在檢查壓縮檔可讀性…", 90));
                Validate(partial, token, (read, length) => progress.Report(new ArchiveProgress("壓縮完成，正在檢查壓縮檔可讀性…", 90 + (int)Math.Min(9, 9.0 * read / Math.Max(1, length)))));
                token.ThrowIfCancellationRequested();
                for (int i = 0; ; i++) {
                    string target = Path.Combine(parent, stem + (i == 0 ? "" : "_v0.0." + i) + ".tar.gz");
                    try { File.Move(partial, target); }
                    catch (IOException) { if (!File.Exists(target) && !Directory.Exists(target)) throw; else continue; }
                    progress.Report(new ArchiveProgress("完成，壓縮檔可讀性檢查通過。", 100));
                    return target;
                }
            } finally { if (File.Exists(partial)) File.Delete(partial); }
        }
    }
}
