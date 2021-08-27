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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.ComponentModel;

namespace TouchProtocolTest
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        SerialCom SerialControl;
        List<UserTouchInfo> listTouch;

        public MainWindow()
        {
            InitializeComponent();

            SerialControl = null;

            cvsTouch.MouseMove += canvas_MouseMove;
            cvsTouch.MouseUp += canvas_MouseUp;
            cvsTouch.MouseDown += canvas_MouseDown;

            listTouch = new List<UserTouchInfo>();
            listTouchData.ItemsSource = listTouch;

            cbSerialPort.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (string item in ports)
            {
                cbSerialPort.Items.Add(item);
            }
        }

        private void btnSerialConnect_Click(object sender, RoutedEventArgs e)
        {
            if((string)btnSerialConnect.Content == "Connect")
            {
                if ((SerialControl != null) && (SerialControl.IsOpen))
                {
                    SerialControl.ClosePort();
                }
                string port = cbSerialPort.SelectedItem.ToString();
                int baudrate = Int32.Parse(lblBaudrate.Text);
                SerialControl = new SerialCom(port, baudrate);
                if (SerialControl.OpenPort())
                {
                    Title = "Touch Test - " + port;
                    btnSerialConnect.Content = "Close";
                    cbSerialPort.IsEnabled = false;
                    lblBaudrate.IsEnabled = false;
                }
                else
                {
                    Title = "Touch Test - disconnected";
                    cbSerialPort.IsEnabled = true;
                    lblBaudrate.IsEnabled = true;
                }
            }
            else
            {
                SerialControl.ClosePort();
                btnSerialConnect.Content = "Connect";
                cbSerialPort.IsEnabled = true;
                lblBaudrate.IsEnabled = true;
            }
        }

        private void btnScreenSizeSet_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.F2)
            {
                cbSerialPort.Items.Clear();
                string[] ports = SerialPort.GetPortNames();
                foreach (string item in ports)
                {
                    cbSerialPort.Items.Add(item);
                }
            }
        }
        

        private void cvsTouch_MouseLeave(object sender, MouseEventArgs e)
        {
            cvsTouch.ReleaseMouseCapture();
        }

        private void canvas_MouseDown(object sender, MouseEventArgs e)
        {
            cvsTouch.CaptureMouse();
        }

        private void canvas_MouseUp(object sender, MouseEventArgs e)
        {
            // we are now no longer drawing
            cvsTouch.ReleaseMouseCapture();
            listTouchData.Items.Refresh();
            listTouchData.SelectedIndex = listTouchData.Items.Count - 1;
            listTouchData.ScrollIntoView(listTouchData.SelectedItem);
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            //if we are not drawing, we don't need to do anything when the mouse moves
            if (!cvsTouch.IsMouseCaptured)
                return;

            Point location = e.GetPosition(cvsTouch);
            
            if((location.X < 0) || (location.Y < 0))
            {
                return;
            }
            if ((location.X > cvsTouch.RenderSize.Width) || (location.Y > cvsTouch.RenderSize.Height))
            {
                return;
            }

            //Console.WriteLine("X:{0} Y {1} W{2} H{3}", location.X, location.Y, cvsTouch.RenderSize.Width, cvsTouch.RenderSize.Height);
            Ellipse myEllipse = new Ellipse();
            SolidColorBrush mySolidColorBrush = new SolidColorBrush();
            mySolidColorBrush.Color = Colors.Black;
            myEllipse.Fill = mySolidColorBrush;
            myEllipse.StrokeThickness = 2;
            myEllipse.Stroke = Brushes.Black;

            Canvas.SetTop(myEllipse, location.Y);
            Canvas.SetLeft(myEllipse, location.X);

            // Set the width and height of the Ellipse.
            myEllipse.Width = 3;
            myEllipse.Height = 3;

            // How to set center of ellipse???

            cvsTouch.Children.Add(myEllipse);
            listTouch.Add(new UserTouchInfo(location.X, location.Y));

            Touch_Send(location);
        }

        private bool Touch_Send(Point point)
        {
            if ((SerialControl != null) && (SerialControl.IsOpen))
            {
                byte[] TxMsg = new byte[16];

                TxMsg[0] = 0x02;
                TxMsg[1] = 0x06;
                TxMsg[2] = 0x60;
                TxMsg[3] = 0x00;
                TxMsg[4] = 0x00;
                TxMsg[5] = 0x00;
                TxMsg[6] = 0x06;

                TxMsg[7] = 0x00;
                TxMsg[8] = 0x91;
                TxMsg[9] = (byte)((int)(point.X) >> 0);
                TxMsg[10] = (byte)((int)(point.X) >> 8);
                TxMsg[11] = (byte)((int)(point.Y) >> 0);
                TxMsg[12] = (byte)((int)(point.Y) >> 8);

                TxMsg[13] = 0x0c;
                TxMsg[14] = 0x0d;
                TxMsg[15] = 0x00;

                SerialControl.SendData(TxMsg, MsgFormat.MSG_STRING);
                return true;
            }

            return false;
        }

        private bool Port_Send(string TxMsg, bool ishex)
        {
            if ((SerialControl != null) && (SerialControl.IsOpen))
            {
                byte[] Msg;
                if (!ishex)
                {
                    Msg = KeyParser.AsciiStringToHexByte(TxMsg);
                    if (Msg == null)
                    {
                        return false;
                    }
                }
                else
                {
                    Msg = KeyParser.HexStringToHexByte(TxMsg);
                    if (Msg == null)
                    {
                        return false;
                    }
                }

                SerialControl.SendData(Msg, MsgFormat.MSG_STRING);
                return true;
            }
            return false;
        }

        private bool Port_Send(byte[] TxMsg)
        {
            if ((SerialControl != null) && (SerialControl.IsOpen))
            {
                SerialControl.SendData(TxMsg, MsgFormat.MSG_STRING);
                return true;
            }
            return false;
        }

        private void btnTouchClear_Click(object sender, RoutedEventArgs e)
        {
            cvsTouch.Children.Clear();
            listTouch.Clear();
            listTouchData.Items.Refresh();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if ((SerialControl != null) && (SerialControl.IsOpen))
            {
                SerialControl.ClosePort();
            }
        }
    }

    public class UserTouchInfo : INotifyPropertyChanged
    {
        public UserTouchInfo(double x, double y)
        {
            Time = DateTime.Now.ToString("hh:mm:ss.fff");
            Type = "Move";
            X = Convert.ToInt32(Math.Round(x)).ToString();
            Y = Convert.ToInt32(Math.Round(y)).ToString();
        }
        public string Time { get; set; }
        public string Type { get; set; }
        public string X { get; set;  }
        public string Y { get; set;  }

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

}
