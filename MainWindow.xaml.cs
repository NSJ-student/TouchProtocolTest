
//#define SEND_SINGLE_VALUE

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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Threading;

namespace TouchProtocolTest
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public enum TouchType {
            TOUCH_DOWN,
            TOUCH_MOVE,
            TOUCH_UP
        };
        InputLog windowInputLog;
        SerialCom SerialControl;
        Socket ClientSock;
        Thread ClientThread;
        ThreadStart ts;
        List<UserTouchInfo> listTouch;
        List<Ellipse> listFinger;
        List<SolidColorBrush> listFingerColor = new List<SolidColorBrush>() { 
            Brushes.Red, 
            Brushes.Yellow, 
            Brushes.Green, 
            Brushes.SkyBlue, 
            Brushes.MediumPurple  
        };
        public double touchObjDiameter;
        public static RoutedCommand serialRefresh = new RoutedCommand();
        int touchIntervalMs;
        int touchLastMs;

        public MainWindow()
        {
            InitializeComponent();

            touchObjDiameter = 7;
            touchIntervalMs = 15;
            touchLastMs = 0;

            txtTouchDiameter.Text = ((int)touchObjDiameter).ToString();
            txtTouchInterval.Text = ((int)touchIntervalMs).ToString();

            cbCommSelect.IsChecked = true;
            lblSerialPort.Visibility = Visibility.Visible;
            cbSerialPort.Visibility = Visibility.Visible;
            lblBaudrate.Visibility = Visibility.Visible;
            txtBaudrate.Visibility = Visibility.Visible;
            btnSerialConnect.Visibility = Visibility.Visible;

            lblServerIP.Visibility = Visibility.Collapsed;
            txtServerIP.Visibility = Visibility.Collapsed;
            lblServerPort.Visibility = Visibility.Collapsed;
            txtServerPort.Visibility = Visibility.Collapsed;
            btnTcpConnect.Visibility = Visibility.Collapsed;

            windowInputLog = new InputLog();
            SerialControl = null;
            ClientSock = null;
            ts = new ThreadStart(RxTcpClientMsg);

            cvsTouch.MouseMove += canvas_MouseMove;
            cvsTouch.MouseUp += canvas_MouseUp;
            cvsTouch.MouseDown += canvas_MouseDown;

            listTouch = new List<UserTouchInfo>();
            listTouchData.ItemsSource = listTouch;

            cbSerialPort.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (string item in ports)
            {
                try
                {
                    SerialPort port = new SerialPort(item, 460800);
                    port.Open();
                    if (port.IsOpen)
                    {
                        cbSerialPort.Items.Add(item);
                        port.Close();
                    }
                }
                catch(Exception ex)
                {

                }
            }

            txtCursorPos.Visibility = Visibility.Hidden;

            // finger panel에 center line 추가
            Line lineVertical = new Line();
            lineVertical.StrokeThickness = 1;
            lineVertical.Stroke = Brushes.White;
            lineVertical.X1 = 75;
            lineVertical.X2 = 75;
            lineVertical.Y1 = 70;
            lineVertical.Y2 = 80;
            cvsFinger.Children.Add(lineVertical);
            Line lineHorizontal = new Line();
            lineHorizontal.StrokeThickness = 1;
            lineHorizontal.Stroke = Brushes.White;
            lineHorizontal.X1 = 70;
            lineHorizontal.X2 = 80;
            lineHorizontal.Y1 = 75;
            lineHorizontal.Y2 = 75;
            cvsFinger.Children.Add(lineHorizontal);

            // finger panel에 finger 추가
            listFinger = new List<Ellipse>();
            listFinger.Add(addFinger(new Point(75, 75)));
            listFinger.Add(addFinger(new Point(75, 75)));
            listFinger.Add(addFinger(new Point(75, 75)));
            listFinger.Add(addFinger(new Point(75, 75)));
            listFinger.Add(addFinger(new Point(75, 75)));
            for (int cnt = 1; cnt < 5; cnt++)
            {
                listFinger[cnt].Visibility = Visibility.Hidden;
            }

            // F2 shortcut (포트 새로고침)
            serialRefresh.InputGestures.Add(new KeyGesture(Key.F2, ModifierKeys.None));
            CommandBindings.Add(new CommandBinding(serialRefresh, serialPortRefresh));
        }


        /**************************/
        //     Protocol
        /**************************/

        private bool Touch_Send(int id, Point point, TouchType type = TouchType.TOUCH_MOVE)
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

                TxMsg[7] = (byte)id;
                if(type == TouchType.TOUCH_UP)
                {
                    TxMsg[8] = 0x95;
                }
                else if(type == TouchType.TOUCH_DOWN)
                {
                    TxMsg[8] = 0x94;
                }
                else if(type == TouchType.TOUCH_MOVE)
                {
                    TxMsg[8] = 0x91;
                }
                else
                {
                    TxMsg[8] = 0x90;
                }

                int x = Convert.ToInt32(Math.Round(point.X));
                int y = Convert.ToInt32(Math.Round(point.Y));
                TxMsg[9]  = (byte)(x >> 0);
                TxMsg[10] = (byte)(x >> 8);
                TxMsg[11] = (byte)(y >> 0);
                TxMsg[12] = (byte)(y >> 8);

                TxMsg[13] = 0x0c;
                TxMsg[14] = 0x0d;
                TxMsg[15] = 0x00;

                SerialControl.SendData(TxMsg, MsgFormat.MSG_STRING);
                return true;
            }

            return false;
        }

        private bool Touch_Send(List<Point> point_list, TouchType type = TouchType.TOUCH_MOVE)
        {
            if(point_list == null)
            {
                return false;
            }

            if ((SerialControl == null) || (!SerialControl.IsOpen))
            {
                if ((ClientSock == null) || (!ClientSock.Connected))
                {
                    return false;
                }
            }

#if SEND_SINGLE_VALUE
            for(int cnt=0; cnt<point_list.Count; cnt++)
            {
                Touch_Send(cnt, point_list[cnt], type);
            }
#else
            int fingers = point_list.Count;
            byte[] TxMsg;

            if(cbChannelEnable.IsChecked == true)
            {
                TxMsg = new byte[fingers * 6 + 11];
                byte channel = Convert.ToByte(txtTouchChannel.Text);
                if(channel > 8)
                {
                    return false;
                }

                TxMsg[0] = 0x02;
                TxMsg[1] = 0x06;
                TxMsg[2] = 0x71;
                TxMsg[3] = 0x00;
                TxMsg[4] = 0x00;
                TxMsg[5] = 0x00;
                TxMsg[6] = (byte)((fingers * 6)+1);

                TxMsg[7] = channel;
                for (int cnt = 0; cnt < fingers; cnt++)
                {
                    TxMsg[(6 * cnt) + 8] = (byte)cnt;
                    if (type == TouchType.TOUCH_UP)
                    {
                        TxMsg[(6 * cnt) + 9] = 0x95;
                    }
                    else if (type == TouchType.TOUCH_DOWN)
                    {
                        TxMsg[(6 * cnt) + 9] = 0x94;
                    }
                    else if (type == TouchType.TOUCH_MOVE)
                    {
                        TxMsg[(6 * cnt) + 9] = 0x91;
                    }
                    else
                    {
                        TxMsg[(6 * cnt) + 9] = 0x90;
                    }

                    int x = Convert.ToInt32(Math.Round(point_list[cnt].X));
                    int y = Convert.ToInt32(Math.Round(point_list[cnt].Y));
                    TxMsg[(6 * cnt) + 10] = (byte)(x >> 0);
                    TxMsg[(6 * cnt) + 11] = (byte)(x >> 8);
                    TxMsg[(6 * cnt) + 12] = (byte)(y >> 0);
                    TxMsg[(6 * cnt) + 13] = (byte)(y >> 8);
                }

                TxMsg[fingers * 6 + 8] = 0x0c;
                TxMsg[fingers * 6 + 9] = 0x0d;
                TxMsg[fingers * 6 + 10] = 0x00;
            }
            else
            {
                TxMsg = new byte[fingers * 6 + 10];
                TxMsg[0] = 0x02;
                TxMsg[1] = 0x06;
                TxMsg[2] = 0x60;
                TxMsg[3] = 0x00;
                TxMsg[4] = 0x00;
                TxMsg[5] = 0x00;
                TxMsg[6] = (byte)(fingers * 6);

                for (int cnt = 0; cnt < fingers; cnt++)
                {
                    TxMsg[(6 * cnt) + 7] = (byte)cnt;
                    if (type == TouchType.TOUCH_UP)
                    {
                        TxMsg[(6 * cnt) + 8] = 0x95;
                    }
                    else if (type == TouchType.TOUCH_DOWN)
                    {
                        TxMsg[(6 * cnt) + 8] = 0x94;
                    }
                    else if (type == TouchType.TOUCH_MOVE)
                    {
                        TxMsg[(6 * cnt) + 8] = 0x91;
                    }
                    else
                    {
                        TxMsg[(6 * cnt) + 8] = 0x90;
                    }

                    int x = Convert.ToInt32(Math.Round(point_list[cnt].X));
                    int y = Convert.ToInt32(Math.Round(point_list[cnt].Y));
                    TxMsg[(6 * cnt) + 9]  = (byte)(x >> 0);
                    TxMsg[(6 * cnt) + 10] = (byte)(x >> 8);
                    TxMsg[(6 * cnt) + 11] = (byte)(y >> 0);
                    TxMsg[(6 * cnt) + 12] = (byte)(y >> 8);
                }

                TxMsg[fingers * 6 + 7] = 0x0c;
                TxMsg[fingers * 6 + 8] = 0x0d;
                TxMsg[fingers * 6 + 9] = 0x00;
            }

            if ((SerialControl != null) && (SerialControl.IsOpen))
            {
                SerialControl.SendData(TxMsg, MsgFormat.MSG_STRING);
                return true;
            }
            if ((ClientSock != null) && (ClientSock.Connected))
            {
                ClientSock.Send(TxMsg);
                return true;
            }
#endif

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

        private void RxTcpClientMsg()
        {
            try
            {
                while (ClientSock.Connected)
                {
                    if (ClientSock.Available > 0)
                    {
                        byte[] data = new byte[ClientSock.Available];
                        int rxLength = ClientSock.Receive(data);
                        if (rxLength == 0)
                        {
                            break;
                        }
                        
                    }
                    Thread.Sleep(1);
                }
                
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    Title = "Touch Test";
                    btnTcpConnect.Content = "Connect";
                    txtServerPort.IsEnabled = true;
                    txtServerIP.IsEnabled = true;
                    ClientSock.Disconnect(false);
                    ClientSock.Close();
                }));
            }
            catch (Exception es)
            {
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    Title = "Touch Test";
                    btnTcpConnect.Content = "Connect";
                    txtServerPort.IsEnabled = true;
                    txtServerIP.IsEnabled = true;
//                    ClientSock.Disconnect(false);
                    ClientSock.Close();
                }));
            }
        }
        
        private bool RxSerialMsg(byte[] recvMsg)
        {
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
            {
                windowInputLog.appendText(Encoding.Default.GetString(recvMsg));
            }));
            return true;
        }

        /**************************/
        //     Touch Panel
        /**************************/

        private void touchPanel_SizeChange()
        {
            int real_width = Convert.ToInt32(txtScreenWidth.Text);
            int real_height = Convert.ToInt32(txtScreenHeight.Text);

            double width = (gridMain.ActualWidth) - 
                (stackControl.Margin.Left + stackControl.Margin.Right + stackControl.ActualWidth) - 5.0;
            double height = (gridMain.ActualHeight) - 
                (stackConnect.Margin.Bottom + stackConnect.Margin.Top + stackConnect.ActualHeight);

            double exp_height = width * real_height / real_width;
            if (exp_height <= height)
            {
                cvsTouch.Width = width;
                cvsTouch.Height = exp_height;
                return;
            }

            double exp_width = height * real_width / real_height;
            if (exp_width <= width)
            {
                cvsTouch.Width = exp_width;
                cvsTouch.Height = height;
                return;
            }
        }

        private void touchPanel_EventOccurred(Point location, TouchType type = TouchType.TOUCH_MOVE)
        {
            double cvs_width = cvsTouch.ActualWidth;
            double cvs_height = cvsTouch.ActualHeight;

            if ((location.X < 0) || (location.Y < 0))
            {
                return;
            }
            if ((location.X > cvs_width) || (location.Y > cvs_height))
            {
                return;
            }

            List<Point> points = finger_relativePosition(location);
            if(cbDrawEnable.IsChecked == true)
            {
                touchObject_Create(points);
            }

            int real_width = Convert.ToInt32(txtScreenWidth.Text);
            int real_height = Convert.ToInt32(txtScreenHeight.Text);

            List<Point> real_points = new List<Point>();
            for (int cnt = 0; cnt < points.Count; cnt++)
            {
                double x = points[cnt].X * real_width / cvs_width;
                double y = points[cnt].Y * real_height / cvs_height;

                real_points.Add(new Point(x, y));
            }

            if (cbToolTipEnable.IsChecked == true)
            {
                touchObject_Tooltip(location, real_points);
            }

            touchObj_PrintInfo(real_points, type);
            Point real_location = new Point(
                location.X * real_width / cvs_width,
                location.Y * real_height / cvs_height);
            touchObj_PrintInfo(real_location, type);

            Touch_Send(real_points, type);
        }

        private void touchObject_Tooltip(Point location, List<Point> real_locations)
        {
            txtCursorPos.Text = "";
            for (int cnt = 0; cnt < real_locations.Count; cnt++)
            {
                if(cnt>0)
                {
                    txtCursorPos.Text += Environment.NewLine;
                }
                txtCursorPos.Text += String.Format("({0:0}, {1:0})", 
                    real_locations[cnt].X, real_locations[cnt].Y);
            }

            if (cvsTouch.ActualWidth > location.X + txtCursorPos.ActualWidth)
            {
                Canvas.SetLeft(txtCursorPos, location.X + 1);
            }
            else
            {
                Canvas.SetLeft(txtCursorPos, location.X - txtCursorPos.ActualWidth - 1);
            }

            if (location.Y < txtCursorPos.ActualHeight)
            {
                Canvas.SetTop(txtCursorPos, location.Y);
            }
            else
            {
                Canvas.SetTop(txtCursorPos, location.Y - 15);
            }

        }

        private void touchObject_Create(List<Point> positions)
        {
            for(int cnt=0; cnt<positions.Count; cnt++)
            {
                Ellipse myEllipse = new Ellipse();
                myEllipse.Fill = listFingerColor[cnt];
                myEllipse.StrokeThickness = 2;
                myEllipse.Stroke = listFingerColor[cnt];

                myEllipse.Width = touchObjDiameter;
                myEllipse.Height = touchObjDiameter;

                Canvas.SetTop(myEllipse, positions[cnt].Y - (touchObjDiameter / 2));
                Canvas.SetLeft(myEllipse, positions[cnt].X - (touchObjDiameter / 2));

                myEllipse.MouseEnter += touchObj_MouseEnter;
                myEllipse.MouseLeave += touchObj_MouseLeave;

                cvsTouch.Children.Add(myEllipse);
            }
        }
        
        private string touchObject_Type(TouchType type)
        {
            if (type == TouchType.TOUCH_UP)
            {
                return "Up";
            }
            else if (type == TouchType.TOUCH_DOWN)
            {
                return "Down";
            }
            else if (type == TouchType.TOUCH_MOVE)
            {
                return "Move";
            }
            else
            {
                return "None";
            }
        }

        private void touchObj_PrintInfo(Point real_location, TouchType type)
        {
            listTouch.Add(new UserTouchInfo(real_location.X, real_location.Y, touchObject_Type(type)));
            if (listTouch.Count > 30)
            {
                listTouch.RemoveAt(0);
            }
        }

        private void touchObj_PrintInfo(List<Point> positions, TouchType type)
        {
            lblTouchType.Content = touchObject_Type(type);

            if (positions.Count > 0)
            {
                lblTouchX1.Content = Convert.ToInt32(Math.Round(positions[0].X)).ToString();
                lblTouchY1.Content = Convert.ToInt32(Math.Round(positions[0].Y)).ToString();
            }
            else
            {
                lblTouchX1.Content = "";
                lblTouchY1.Content = "";
            }

            if (positions.Count > 1)
            {
                lblTouchX2.Content = Convert.ToInt32(Math.Round(positions[1].X)).ToString();
                lblTouchY2.Content = Convert.ToInt32(Math.Round(positions[1].Y)).ToString();
            }
            else
            {
                lblTouchX2.Content = "";
                lblTouchY2.Content = "";
            }

            if (positions.Count > 2)
            {
                lblTouchX3.Content = Convert.ToInt32(Math.Round(positions[2].X)).ToString();
                lblTouchY3.Content = Convert.ToInt32(Math.Round(positions[2].Y)).ToString();
            }
            else
            {
                lblTouchX3.Content = "";
                lblTouchY3.Content = "";
            }

            if (positions.Count > 3)
            {
                lblTouchX4.Content = Convert.ToInt32(Math.Round(positions[3].X)).ToString();
                lblTouchY4.Content = Convert.ToInt32(Math.Round(positions[3].Y)).ToString();
            }
            else
            {
                lblTouchX4.Content = "";
                lblTouchY4.Content = "";
            }

            if (positions.Count > 4)
            {
                lblTouchX5.Content = Convert.ToInt32(Math.Round(positions[4].X)).ToString();
                lblTouchY5.Content = Convert.ToInt32(Math.Round(positions[4].Y)).ToString();
            }
            else
            {
                lblTouchX5.Content = "";
                lblTouchY5.Content = "";
            }
        }

        /*****************/
        //  Touch Event
        /*****************/

        private void touchObj_MouseEnter(object sender, MouseEventArgs e)
        {
            if (cvsTouch.IsMouseCaptured)
                return;

            Point location = e.GetPosition(cvsTouch);

            Ellipse selected = sender as Ellipse;
            double x = Canvas.GetLeft(selected) + (touchObjDiameter / 2);
            double y = Canvas.GetTop(selected) + (touchObjDiameter / 2);
            int real_width = Convert.ToInt32(txtScreenWidth.Text);
            int real_height = Convert.ToInt32(txtScreenHeight.Text);
            double cvs_width = cvsTouch.ActualWidth;
            double cvs_height = cvsTouch.ActualHeight;
            Point real_location = new Point(
                x * real_width / cvs_width,
                y * real_height / cvs_height);

            touchObject_Tooltip(location, new List<Point>() { real_location });
            txtCursorPos.Visibility = Visibility.Visible;
        }

        private void touchObj_MouseLeave(object sender, MouseEventArgs e)
        {
            if (cvsTouch.IsMouseCaptured)
                return;

            txtCursorPos.Visibility = Visibility.Hidden;
        }

        private void cvsTouch_MouseLeave(object sender, MouseEventArgs e)
        {
            cvsTouch.ReleaseMouseCapture();
            if (cbToolTipEnable.IsChecked == true)
            {
                txtCursorPos.Visibility = Visibility.Hidden;
            }
        }

        private void canvas_MouseDown(object sender, MouseEventArgs e)
        {
            Point location = e.GetPosition(cvsTouch);
            touchPanel_EventOccurred(location, TouchType.TOUCH_DOWN);

            cvsTouch.CaptureMouse();
            if (cbToolTipEnable.IsChecked == true)
            {
                txtCursorPos.Visibility = Visibility.Visible;
            }
        }

        private void canvas_MouseUp(object sender, MouseEventArgs e)
        {
            Point location = e.GetPosition(cvsTouch);
            touchPanel_EventOccurred(location, TouchType.TOUCH_UP);

            // we are now no longer drawing
            cvsTouch.ReleaseMouseCapture();
            if (cbToolTipEnable.IsChecked == true)
            {
                txtCursorPos.Visibility = Visibility.Hidden;
            }
            listTouchData.Items.Refresh();
            listTouchData.SelectedIndex = listTouchData.Items.Count - 1;
            listTouchData.ScrollIntoView(listTouchData.SelectedItem);
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!cvsTouch.IsMouseCaptured)
                return;

            if (touchIntervalMs > 0)
            {
                if (((Environment.TickCount & Int32.MaxValue) - touchLastMs) < touchIntervalMs)
                {
                    return;
                }
                touchLastMs = (Environment.TickCount & Int32.MaxValue);
            }

            Point location = e.GetPosition(cvsTouch);
            touchPanel_EventOccurred(location);
        }

        private void btnTouchClear_Click(object sender, RoutedEventArgs e)
        {
            cvsTouch.Children.Clear();
            cvsTouch.Children.Add(txtCursorPos);
            listTouch.Clear();
            listTouchData.Items.Refresh();

            lblTouchType.Content = "";
            lblTouchX1.Content = "";
            lblTouchY1.Content = "";
            lblTouchX2.Content = "";
            lblTouchY2.Content = "";
            lblTouchX3.Content = "";
            lblTouchY3.Content = "";
            lblTouchX4.Content = "";
            lblTouchY4.Content = "";
            lblTouchX5.Content = "";
            lblTouchY5.Content = "";
        }

        private void btnScreenSizeSet_Click(object sender, RoutedEventArgs e)
        {
            touchPanel_SizeChange();
        }


        /**************************/
        //     Finger Panel
        /**************************/

        private Ellipse addFinger(Point position)
        {
            Ellipse myEllipse = new Ellipse();
            myEllipse.Fill = listFingerColor[listFinger.Count];
            myEllipse.StrokeThickness = 2;
            myEllipse.Stroke = listFingerColor[listFinger.Count];

            myEllipse.Width = 10;
            myEllipse.Height = 10;

            Canvas.SetTop(myEllipse, position.Y-5);
            Canvas.SetLeft(myEllipse, position.X-5);

            myEllipse.MouseDown += finger_MouseDown;
            myEllipse.MouseMove += finger_MouseMove;
            myEllipse.MouseUp += finger_MouseUp;

            cvsFinger.Children.Add(myEllipse);

            return myEllipse;
        }

        private List<Point> finger_relativePosition(Point center)
        {
            List<Point> list = new List<Point>();

            ComboBoxItem item = cbFingerCount.SelectedItem as ComboBoxItem;
            int finger = Convert.ToInt32(item.Content);

            for (int cnt = 0; cnt < finger; cnt++)
            {
                double x = center.X + Canvas.GetLeft(listFinger[cnt]) - 70;
                if (x < 0) x = 0;
                if (x > cvsTouch.ActualWidth) x = cvsTouch.ActualWidth;

                double y = center.Y + Canvas.GetTop(listFinger[cnt]) - 70;
                if (y < 0) y = 0;
                if (y > cvsTouch.ActualHeight) y = cvsTouch.ActualHeight;

                list.Add(new Point(x, y));
            }

            return list;
        }

        /*****************/
        //  Finger Event
        /*****************/

        private void finger_MouseDown(object sender, MouseEventArgs e)
        {
            Ellipse selected = sender as Ellipse;
            selected.CaptureMouse();
        }

        private void finger_MouseUp(object sender, MouseEventArgs e)
        {
            Ellipse selected = sender as Ellipse;
            selected.ReleaseMouseCapture();
        }

        private void finger_MouseMove(object sender, MouseEventArgs e)
        {
            Ellipse selected = sender as Ellipse;
            //if we are not drawing, we don't need to do anything when the mouse moves
            if (!selected.IsMouseCaptured)
                return;

            Point location = e.GetPosition(cvsFinger);
            Canvas.SetTop(selected, location.Y - 5);
            Canvas.SetLeft(selected, location.X - 5);
        }

        private void cbFingerCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listFinger == null)
            {
                return;
            }
            ComboBoxItem item = cbFingerCount.SelectedItem as ComboBoxItem;
            int finger = Convert.ToInt32(item.Content);

            for (int cnt = 0; cnt < 5; cnt++)
            {
                if (finger >= cnt + 1)
                {
                    listFinger[cnt].Visibility = Visibility.Visible;
                }
                else
                {
                    listFinger[cnt].Visibility = Visibility.Hidden;
                }
            }
        }

        private void btnFingerDefault_Click(object sender, RoutedEventArgs e)
        {
            for (int cnt = 0; cnt < 5; cnt++)
            {
                Canvas.SetTop(listFinger[cnt], 70);
                Canvas.SetLeft(listFinger[cnt], 70);
            }
        }


        /**************************/
        //     UI Options
        /**************************/

        private void cbShowMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(cbShowMethod.SelectedIndex == 0)
            {
                tabShowMethod.SelectedIndex = 0;
            }
            else
            {
                tabShowMethod.SelectedIndex = 1;
            }
        }

        private void txtTouchDiameter_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                try
                {
                    int touch_diameter = Convert.ToInt32(txtTouchDiameter.Text);

                    if (touch_diameter <= 15)
                    {
                        touchObjDiameter = touch_diameter;
                    }
                    else if (touch_diameter <= 1)
                    {
                        touchObjDiameter = 2;
                        txtTouchDiameter.Text = "2";
                    }
                    else
                    {
                        touchObjDiameter = 15;
                        txtTouchDiameter.Text = "15";
                    }
                }
                catch
                {
                    touchObjDiameter = 3;
                    txtTouchDiameter.Text = "3";
                }
            }
        }
        
        private void btnBackgroundImage_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Image File(*.jpg;*.png;*.bmp)|*.jpg;*.png;*.bmp";
            if (dialog.ShowDialog() == true)
            {
                ImageBrush ib = new ImageBrush();
                ib.ImageSource = new BitmapImage(new Uri(dialog.FileName, UriKind.Relative));
                cvsTouch.Background = ib;
            }
        }

        private void btnBackgroundColor_Click(object sender, RoutedEventArgs e)
        {
            cvsTouch.Background = Brushes.Black;
        }

        private void repositionLogWindow()
        {
            var location = this.PointToScreen(new Point(0, 0));
            windowInputLog.Left = location.X + this.Width;
            windowInputLog.Top = location.Y;
        }

        /**************************/
        //     UI Event
        /**************************/

        private void cbCommSelect_Checked(object sender, RoutedEventArgs e)
        {
            if ((ClientSock != null) && (ClientSock.Connected))
            {
                Title = "Touch Test";
                btnTcpConnect.Content = "Connect";
                txtServerPort.IsEnabled = true;
                txtServerIP.IsEnabled = true;
                ClientSock.Disconnect(false);
                ClientSock.Close();
                if (ClientThread.IsAlive)
                {
                    ClientThread.Abort();
                }
            }
            lblSerialPort.Visibility = Visibility.Visible;
            cbSerialPort.Visibility = Visibility.Visible;
            lblBaudrate.Visibility = Visibility.Visible;
            txtBaudrate.Visibility = Visibility.Visible;
            btnSerialConnect.Visibility = Visibility.Visible;

            lblServerIP.Visibility = Visibility.Collapsed;
            txtServerIP.Visibility = Visibility.Collapsed;
            lblServerPort.Visibility = Visibility.Collapsed;
            txtServerPort.Visibility = Visibility.Collapsed;
            btnTcpConnect.Visibility = Visibility.Collapsed;
        }

        private void cbCommSelect_Unchecked(object sender, RoutedEventArgs e)
        {
            if ((SerialControl != null) && (SerialControl.IsOpen))
            {
                Title = "Touch Test";
                btnSerialConnect.Content = "Connect";
                cbSerialPort.IsEnabled = true;
                txtBaudrate.IsEnabled = true;
                SerialControl.ClosePort();
            }
            lblSerialPort.Visibility = Visibility.Collapsed;
            cbSerialPort.Visibility = Visibility.Collapsed;
            lblBaudrate.Visibility = Visibility.Collapsed;
            txtBaudrate.Visibility = Visibility.Collapsed;
            btnSerialConnect.Visibility = Visibility.Collapsed;

            lblServerIP.Visibility = Visibility.Visible;
            txtServerIP.Visibility = Visibility.Visible;
            lblServerPort.Visibility = Visibility.Visible;
            txtServerPort.Visibility = Visibility.Visible;
            btnTcpConnect.Visibility = Visibility.Visible;
        }

        private void btnTcpConnect_Click(object sender, RoutedEventArgs e)
        {
            if ((string)btnTcpConnect.Content == "Connect")
            {
                if ((ClientSock != null) && (ClientSock.Connected))
                {
                    ClientSock.Close();
                }

                IPAddress ip = IPAddress.Parse(txtServerIP.Text);
                int port = Convert.ToInt32(txtServerPort.Text);
                IPEndPoint ClientIP = new IPEndPoint(ip, port);

                try
                {
                    ClientSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    ClientSock.Connect(ClientIP);
                    ClientThread = new Thread(ts);
                    ClientThread.Start();

                    Title = "Touch Test - " + txtServerIP.Text;
                    btnTcpConnect.Content = "Close";
                    txtServerPort.IsEnabled = false;
                    txtServerIP.IsEnabled = false;
                }
                catch (Exception es)
                {
                    Title = "Touch Test - connect failed";
                    txtServerPort.IsEnabled = true;
                    txtServerIP.IsEnabled = true;
                }
            }
            else
            {
                if (ClientSock.Connected)
                {
                    ClientSock.Disconnect(false);
                    ClientSock.Close();
                }
                if (ClientThread.IsAlive)
                {
                    ClientThread.Abort();
                }
                btnTcpConnect.Content = "Connect";
                Title = "Touch Test";
                txtServerPort.IsEnabled = true;
                txtServerIP.IsEnabled = true;
            }
        }

        private void btnSerialConnect_Click(object sender, RoutedEventArgs e)
        {
            if ((string)btnSerialConnect.Content == "Connect")
            {
                if(cbSerialPort.Items.Count == 0)
                {
                    return;
                }
                if ((SerialControl != null) && (SerialControl.IsOpen))
                {
                    SerialControl.ClosePort();
                }
                string port = cbSerialPort.SelectedItem.ToString();
                int baudrate = Int32.Parse(txtBaudrate.Text);
                SerialControl = new SerialCom(port, baudrate);
                SerialControl.OnRecvMsg += RxSerialMsg;
                if (SerialControl.OpenPort())
                {
                    Title = "Touch Test - " + port;
                    btnSerialConnect.Content = "Close";
                    cbSerialPort.IsEnabled = false;
                    txtBaudrate.IsEnabled = false;
                }
                else
                {
                    Title = "Touch Test - connect failed";
                    cbSerialPort.IsEnabled = true;
                    txtBaudrate.IsEnabled = true;
                }
            }
            else
            {
                SerialControl.ClosePort();
                btnSerialConnect.Content = "Connect";
                Title = "Touch Test";
                cbSerialPort.IsEnabled = true;
                txtBaudrate.IsEnabled = true;
            }
        }

        private void btnShowLog_Click(object sender, RoutedEventArgs e)
        {
            if(windowInputLog.IsVisible)
            {
                windowInputLog.Hide();
            }
            else
            {
                repositionLogWindow();
                windowInputLog.Show();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                cbSerialPort.Items.Clear();
                string[] ports = SerialPort.GetPortNames();
                foreach (string item in ports)
                {
                    cbSerialPort.Items.Add(item);
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if ((SerialControl != null) && (SerialControl.IsOpen))
            {
                SerialControl.ClosePort();
            }

            if ((ClientSock != null) && (ClientSock.Connected))
            {
                ClientSock.Close();
            }

            windowInputLog.terminateApp = true;
            windowInputLog.Close();
        }
        
        private void mainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            touchPanel_SizeChange();
            repositionLogWindow();
        }

        private void StackPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            touchPanel_SizeChange();
        }

        private void serialPortRefresh(object sender, ExecutedRoutedEventArgs e)
        {
            if ((SerialControl == null) || (!SerialControl.IsOpen))
            {
                cbSerialPort.Items.Clear();
                string[] ports = SerialPort.GetPortNames();
                foreach (string item in ports)
                {
                    try
                    {
                        SerialPort port = new SerialPort(item, 460800);
                        port.Open();
                        if (port.IsOpen)
                        {
                            cbSerialPort.Items.Add(item);
                            port.Close();
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
        }

        private void txtScreenWidth_GotFocus(object sender, RoutedEventArgs e)
        {
            txtScreenWidth.SelectAll();
        }

        private void txtScreenHeight_GotFocus(object sender, RoutedEventArgs e)
        {
            txtScreenHeight.SelectAll();
        }

        private void txtScreenHeight_KeyUp(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                touchPanel_SizeChange();
            }
        }

        private void txtScreenWidth_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                touchPanel_SizeChange();
            }
        }

        private void txtTouchInterval_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                touchIntervalMs = Convert.ToInt32(txtTouchInterval.Text);
            }
        }

        private void mainWindow_LocationChanged(object sender, EventArgs e)
        {
            if (windowInputLog.IsVisible)
            {
                repositionLogWindow();
            }
        }
    }

    public class UserTouchInfo : INotifyPropertyChanged
    {
        public UserTouchInfo(double x, double y, string type = "Move")
        {
            Time = DateTime.Now.ToString("hh:mm:ss.fff");
            Type = type;
            X = Convert.ToInt32(Math.Round(x)).ToString();
            Y = Convert.ToInt32(Math.Round(y)).ToString();
        }
        public string Time { get; }
        public string Type { get; }
        public string X { get;  }
        public string Y { get;  }

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
