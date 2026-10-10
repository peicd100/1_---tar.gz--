using System;
using System.IO;
using System.Text;
class SelectionProbe {
    static int Main(string[] args) {
        if (args.Length != 2 || args[0] != "--manifest") return 1;
        string[] paths = File.ReadAllLines(args[1], Encoding.Unicode);
        File.WriteAllLines(Environment.GetEnvironmentVariable("PEICD_TEST_REPORT"), paths, new UTF8Encoding(false));
        File.Delete(args[1]); return 0;
    }
}
