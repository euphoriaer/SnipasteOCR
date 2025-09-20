using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SnipasteOCR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartSnipping_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            var overlay = new ScreenCaptureOverlay();
            overlay.Show();

            overlay.OnCaptureCompleted += (image,rect) =>
            {
                if (image != null)
                {
                    var floating = new FloatingImageWindow(image, rect);
                    floating.Show();
                }
                Dispatcher.InvokeAsync(() => Show()); // 确保主线程显示
            };
        }
    }
}