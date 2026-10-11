using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NeX_Drop.Service.Hooks
{
    public sealed class KeyboardHookService : BackgroundService
    {
        private readonly ILogger<KeyboardHookService> _log;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc? _proc; // keep ref to prevent GC
        private IntPtr _hook = IntPtr.Zero;

        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc fn, IntPtr mod, uint tid);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string m);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int k);

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLL { public uint vk, scan, flags, time; public IntPtr extra; }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG { public IntPtr hwnd; public uint msg; public IntPtr w, l; public uint t; public System.Drawing.Point pt; }

        [DllImport("user32.dll")] static extern bool GetMessage(out MSG m, IntPtr h, uint f, uint t);
        [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
        [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG m);

        public KeyboardHookService(ILogger<KeyboardHookService> log) => _log = log;

        protected override Task ExecuteAsync(CancellationToken ct)
        {
            _proc = Callback;
            using var proc = Process.GetCurrentProcess();
            using var mod  = proc.MainModule!;
            _hook = SetWindowsHookEx(13, _proc, GetModuleHandle(mod.ModuleName!), 0);

            if (_hook == IntPtr.Zero) _log.LogError("[KeyHook] Install failed");
            else _log.LogInformation("[KeyHook] Installed");

            ct.Register(() => { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; });

            // Message pump — required for low-level hook
            while (!ct.IsCancellationRequested)
            {
                if (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
            return Task.CompletedTask;
        }

        private IntPtr Callback(int n, IntPtr w, IntPtr l)
        {
            if (n >= 0 && (w == 0x100 || w == 0x104)) // WM_KEYDOWN / WM_SYSKEYDOWN
            {
                var kb = Marshal.PtrToStructure<KBDLL>(l);
                int vk = (int)kb.vk;
                bool alt   = (GetAsyncKeyState(0xA4) & 0x8000) != 0 || (GetAsyncKeyState(0xA5) & 0x8000) != 0;
                bool ctrl  = (GetAsyncKeyState(0xA2) & 0x8000) != 0 || (GetAsyncKeyState(0xA3) & 0x8000) != 0;
                bool shift = (GetAsyncKeyState(0xA0) & 0x8000) != 0 || (GetAsyncKeyState(0xA1) & 0x8000) != 0;

                // Block: Win, Alt+Tab, Alt+F4, Ctrl+Shift+Esc, Ctrl+Esc
                if (vk == 0x5B || vk == 0x5C)                             return (IntPtr)1;
                if (alt   && vk == 0x09)                                   return (IntPtr)1;
                if (alt   && vk == 0x73)                                   return (IntPtr)1;
                if (ctrl  && shift && vk == 0x1B)                          return (IntPtr)1;
                if (ctrl  && vk == 0x1B)                                   return (IntPtr)1;
            }
            return CallNextHookEx(_hook, n, w, l);
        }
    }
}
