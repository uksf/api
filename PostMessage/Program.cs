using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace PostMessage {
    static class Program {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int PostMessage(IntPtr hwnd, int msg, int wparam, int lparam);
        
        public static void Main(string[] args) {
            Process process = Process.GetProcesses().FirstOrDefault(x => x.ProcessName == args[0]);
            if (process == null) return;
            PostMessage(process.MainWindowHandle, int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]));
        }
    }
}
