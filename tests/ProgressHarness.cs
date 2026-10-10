using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Peicd.TarGz;
class ProgressHarness {
    sealed class Recorder : IProgress<ArchiveProgress> {
        internal readonly List<ArchiveProgress> Events = new List<ArchiveProgress>();
        internal Action<ArchiveProgress> Hook;
        public void Report(ArchiveProgress value) { Events.Add(value); if (Hook != null) Hook(value); }
    }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static void Verify(Recorder recorder, bool completed) {
        int previous = -1;
        foreach (var p in recorder.Events) {
            Assert(p.Percent >= 0 && p.Percent <= 100, "out of range");
            Assert(p.Percent >= previous, "progress went backwards"); previous = p.Percent;
        }
        Assert(recorder.Events.First().Percent == 0, "must start at zero");
        if (completed) {
            Assert(recorder.Events.Last().Percent == 100, "successful job must reach 100");
            Assert(recorder.Events.Take(recorder.Events.Count - 1).All(p => p.Percent < 100), "100 reported prematurely");
            Assert(recorder.Events.Any(p => p.Percent == 90 && p.Status.Contains("檢查")), "missing verification phase");
            Assert(recorder.Events.Any(p => p.Percent == 99), "missing verified phase");
        } else Assert(recorder.Events.All(p => p.Percent < 100), "failed/cancelled job reached 100");
    }
    static int Main(string[] args) {
        try {
            string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
            var random = new Random(721); byte[] bytes = new byte[4 * 1024 * 1024]; random.NextBytes(bytes);
            string source = Path.Combine(root, "measured.bin"); File.WriteAllBytes(source, bytes);
            var measured = new Recorder();
            measured.Hook = p => { if (p.Percent == 100) Assert(File.Exists(source + ".tar.gz"), "100 before publication"); };
            Archive.Create(new[] { source }, measured, CancellationToken.None).GetAwaiter().GetResult(); Verify(measured, true);
            Assert(measured.Events.Any(p => p.Percent > 0 && p.Percent < 90), "no intermediate byte progress");
            Console.WriteLine("Measured file: intermediate values, monotonic range, verification, 100 after publication PASS");
            string empty = Path.Combine(root, "empty-folder"); Directory.CreateDirectory(empty); File.WriteAllBytes(Path.Combine(empty, "empty.txt"), new byte[0]);
            var zero = new Recorder(); Archive.Create(new[] { empty }, zero, CancellationToken.None).GetAwaiter().GetResult(); Verify(zero, true);
            Console.WriteLine("Empty file/folder: reaches completion without division by zero PASS");
            var stopped = new Recorder();
            using (var cancel = new CancellationTokenSource()) {
                stopped.Hook = p => { if (p.Percent >= 10) cancel.Cancel(); };
                bool caught = false;
                try { Archive.Create(new[] { source }, stopped, cancel.Token).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { caught = true; }
                Assert(caught, "cancellation ignored"); Verify(stopped, false);
                Assert(!File.Exists(source + "_v0.0.1.tar.gz"), "cancelled output published");
            }
            Assert(!Directory.GetFiles(root, "*.partial").Any(), "partial leaked");
            Console.WriteLine("Cancellation: never 100, no published/partial output PASS");
            var failed = new Recorder(); bool error = false;
            try { Archive.Create(new[] { Path.Combine(root, "missing") }, failed, CancellationToken.None).GetAwaiter().GetResult(); }
            catch (FileNotFoundException) { error = true; }
            Assert(error, "missing input accepted"); Verify(failed, false);
            Console.WriteLine("Failure: never claims completion PASS");
            File.WriteAllLines(Path.Combine(root, "percentages.txt"), measured.Events.Select(p => p.Percent + "% " + p.Status));
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
