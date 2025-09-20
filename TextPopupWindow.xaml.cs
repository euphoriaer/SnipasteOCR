using System.Windows;

namespace SnipasteOCR
{
    public partial class TextPopupWindow : Window
    {
        public TextPopupWindow(string text = "")
        {
            InitializeComponent();
            ResultTextBox.Text = text;
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ResultTextBox.Text);
            MessageBox.Show("已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}