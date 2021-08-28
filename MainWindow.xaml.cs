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
using LiveCharts;
using LiveCharts.Defaults;

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

            //범례 위치 설정
            graphTouch.LegendLocation = LiveCharts.LegendLocation.Top;
            //세로 눈금 값 설정
            graphTouch.AxisY.Add(new LiveCharts.Wpf.Axis { MinValue = 0, MaxValue = 1080 });
            //가로 눈금 값 설정
            graphTouch.AxisX.Add(new LiveCharts.Wpf.Axis { MinValue = 0, MaxValue = 1920 });

            touchObjects.Values = new ChartValues<ObservablePoint>();
            
            SerialControl = null;
            
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
                    Title = "Touch Test - connect failed";
                    cbSerialPort.IsEnabled = true;
                    lblBaudrate.IsEnabled = true;
                }
            }
            else
            {
                SerialControl.ClosePort();
                btnSerialConnect.Content = "Connect";
                Title = "Touch Test";
                cbSerialPort.IsEnabled = true;
                lblBaudrate.IsEnabled = true;
            }
        }

        private void btnScreenSizeSet_Click(object sender, RoutedEventArgs e)
        {
            int width = Convert.ToInt32(txtScreenWidth.Text);
            int height = Convert.ToInt32(txtScreenHeight.Text);
            
            graphTouch.AxisX[0].MaxValue = width;
            graphTouch.AxisY[0].MaxValue = height;
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
            touchObjects.Values.Clear();
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

        private void touchObjects_MouseDown(object sender, MouseButtonEventArgs e)
        {
            touchObjects.CaptureMouse();

            Point location = e.GetPosition(touchObjects);

            if ((location.X < 0) || (location.Y < 0))
            {
                return;
            }
            if ((location.X > Convert.ToInt32(txtScreenWidth.Text)) ||
                (location.Y > Convert.ToInt32(txtScreenHeight.Text)))
            {
                return;
            }

            touchObjects.Values.Add(new ObservablePoint(location.X, location.Y));
            listTouch.Add(new UserTouchInfo(location.X, location.Y));

            Touch_Send(location);
        }

        private void touchObjects_MouseMove(object sender, MouseEventArgs e)
        {
            if (!touchObjects.IsMouseCaptured)
                return;

            Point location = e.GetPosition(touchObjects);

            if ((location.X < 0) || (location.Y < 0))
            {
                return;
            }
            if ((location.X > Convert.ToInt32(txtScreenWidth.Text)) ||
                (location.Y > Convert.ToInt32(txtScreenHeight.Text)))
            {
                return;
            }

            touchObjects.Values.Add(new ObservablePoint(location.X, location.Y));
            listTouch.Add(new UserTouchInfo(location.X, location.Y));

            Touch_Send(location);
        }

        private void touchObjects_MouseLeave(object sender, MouseEventArgs e)
        {
            touchObjects.ReleaseMouseCapture();
        }

        private void touchObjects_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Point location = e.GetPosition(touchObjects);

            if ((location.X < 0) || (location.Y < 0))
            {
                return;
            }
            if ((location.X > Convert.ToInt32(txtScreenWidth.Text)) ||
                (location.Y > Convert.ToInt32(txtScreenHeight.Text)))
            {
                return;
            }

            touchObjects.Values.Add(new ObservablePoint(location.X, location.Y));
            listTouch.Add(new UserTouchInfo(location.X, location.Y));

            Touch_Send(location);

            // we are now no longer drawing
            touchObjects.ReleaseMouseCapture();
            listTouchData.Items.Refresh();
            listTouchData.SelectedIndex = listTouchData.Items.Count - 1;
            listTouchData.ScrollIntoView(listTouchData.SelectedItem);
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
