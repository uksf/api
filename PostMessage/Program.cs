using System;
using System.Runtime.InteropServices;

namespace PostMessage {
    static class Program {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int PostMessage(IntPtr hwnd, int msg, int wparam, int lparam);
        
        public static void Main(string[] args) {
            PostMessage(new IntPtr(int.Parse(args[0])), int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]));
        }
    }
}
