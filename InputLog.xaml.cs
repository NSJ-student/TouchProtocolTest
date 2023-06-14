using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TouchProtocolTest
{
    /// <summary>
    /// InputLog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class InputLog : Window
    {
        public bool terminateApp;

        public InputLog()
        {
            InitializeComponent();

            this.Hide();
            terminateApp = false;
        }

        public void appendText(string text)
        {
            txtInputLog.Text += text;
            scrollViewer.ScrollToBottom();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(!terminateApp)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void btnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtInputLog.Text = "";
        }
    }
}
