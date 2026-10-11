// ShellWindow code-behind
// PasswordBox cannot bind directly in MVVM — handled here via PasswordChanged event.

using NeX_Drop.Shell.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace NeX_Drop.Shell.Views
{
    public partial class ShellWindow : Window
    {
        private readonly ShellViewModel _vm;

        public ShellWindow(ShellViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            Closing += (s, e) => e.Cancel = true; // Cannot be closed by local user
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Block system commands at WndProc level
            (PresentationSource.FromVisual(this) as HwndSource)?.AddHook(WndProc);

            // Cover all monitors
            Left   = SystemParameters.VirtualScreenLeft;
            Top    = SystemParameters.VirtualScreenTop;
            Width  = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0112) // WM_SYSCOMMAND
            {
                int cmd = wParam.ToInt32() & 0xFFF0;
                // Block Close, Minimize, Maximize, Move
                if (cmd == 0xF060 || cmd == 0xF020 || cmd == 0xF030 || cmd == 0xF010)
                { handled = true; return IntPtr.Zero; }
            }
            return IntPtr.Zero;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            bool alt  = Keyboard.IsKeyDown(Key.LeftAlt)  || Keyboard.IsKeyDown(Key.RightAlt);
            bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool shift= Keyboard.IsKeyDown(Key.LeftShift)|| Keyboard.IsKeyDown(Key.RightShift);

            if (alt  && e.Key == Key.F4)                    { e.Handled = true; return; }
            if (alt  && e.Key == Key.Tab)                   { e.Handled = true; return; }
            if (ctrl && e.Key == Key.Escape)                { e.Handled = true; return; }
            if (ctrl && shift && e.Key == Key.Escape)       { e.Handled = true; return; }
            if (e.Key == Key.LWin || e.Key == Key.RWin)    { e.Handled = true; }
        }

        // ── PasswordBox event handlers (MVVM workaround) ──────────────────────
        private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.PasswordBox pb)
                _vm.AdminPassword = pb.Password;
        }

        private void PwdBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Enter key triggers login
            if (e.Key == Key.Enter)
                _vm.AdminLoginCommand.Execute(null);
        }
    }
}
