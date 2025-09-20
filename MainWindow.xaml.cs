using Hardcodet.Wpf.TaskbarNotification;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace SnipasteOCR
{
    public partial class MainWindow : Window
    {
        private TaskbarIcon notifyIcon;
        private const int WM_HOTKEY = 0x0312;
        private const int PRINT_SCREEN_ID = 9001; // 自定义热键ID
        private const uint VK_SNAPSHOT = 0x2C; // PrintScreen 虚拟键码

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public MainWindow()
        {
            InitializeComponent();
        }



        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 延迟创建托盘，避免初始化过早
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    notifyIcon = new TaskbarIcon
                    {
                        Icon = new System.Drawing.Icon("Resource/ocr.ico"),
                        ToolTipText = "SnipasteOCR - 就绪",
                        ContextMenu = (ContextMenu)FindResource("TrayMenu")
                    };

                    var helper = new WindowInteropHelper(this);
                    var hWnd = helper.EnsureHandle();
                    RegisterHotKey(hWnd, PRINT_SCREEN_ID, 0, VK_SNAPSHOT);

                    ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;

                    // 弹出提示，确认托盘已运行
                    notifyIcon.ShowBalloonTip("SnipasteOCR", "已启动，按 PRTSC 开始截图。", BalloonIcon.Info);

                }
                catch (Exception ex)
                {
                    MessageBox.Show("初始化失败: " + ex.Message);
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (!handled && msg.message == WM_HOTKEY && (int)msg.wParam == PRINT_SCREEN_ID)
            {
                TakeScreenshot();
                handled = true;
            }
        }

        private void TakeScreenshot()
        {
            this.Hide(); // 隐藏窗口（虽然已经是0大小）

            var overlay = new ScreenCaptureOverlay();
            overlay.Show();

            overlay.OnCaptureCompleted += (image, rect) =>
            {
                if (image != null)
                {
                    var floating = new FloatingImageWindow(image, rect);
                    floating.Show();
                }

                // 回到主线程显示主窗口（虽然不可见）
                this.Dispatcher.InvokeAsync(() => this.Show());
            };
        }

        private void TakeScreenshot_Click(object sender, RoutedEventArgs e)
        {
            TakeScreenshot();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            ShutdownApplication();
        }

        private void ShutdownApplication()
        {
            // 注销热键
            UnregisterHotKey(new WindowInteropHelper(this).Handle, PRINT_SCREEN_ID);
            ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;

            // 销毁托盘图标
            notifyIcon?.Dispose();

            Application.Current.Shutdown();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ShutdownApplication();
        }

        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {

        }
    }
}