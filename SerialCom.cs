using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;
using System.IO;
using System.Xml.Linq;

namespace TouchProtocolTest
{
	class SerialCom : IComm
	{
		SerialPort PortInfo;
		Thread rxThread;
        ThreadStart ts;
        public event ReceiveHandler OnRecvMsg;
		public event OpenCloseHandler OnOpenClose;
		public event LogHandler OnLogAdded;

        public CommType Type
		{
			get
			{
				return CommType.COMM_SERIAL;
			}
		}
		public bool IsOpen
		{
			get
			{
				return (PortInfo != null) ? PortInfo.IsOpen : false;
			}
		}
		public object CurrentPort
		{
			get
			{
				return PortInfo;
			}
		}
		public SerialCom()
		{
            OnOpenClose = null;
            OnRecvMsg = null;
            OnLogAdded = null;
            ts = new ThreadStart(ReadMsg);
//			rxThread = new Thread(ts);
		}
		public SerialCom(string PortName, int BaudRate)
			: this()
		{
			PortInfo = new SerialPort(PortName);
			PortInfo.BaudRate = BaudRate;
			PortInfo.DataBits = 8;
			PortInfo.StopBits = StopBits.One;
			PortInfo.Parity = Parity.None;
            PortInfo.RtsEnable = false;
        }
		public SerialCom(SerialPort port)
			: this()
		{
			PortInfo = port;
		}

        public bool OpenPort()
        {
            try
            {
                PortInfo.ReadTimeout = 100;
                PortInfo.WriteTimeout = 100;
                PortInfo.Open();
                if(OnOpenClose!=null) OnOpenClose(true);
                rxThread = new Thread(ts);
                rxThread.Start();
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }
        public bool OpenPort(object port)
		{
			PortInfo = port as SerialPort;

			try
			{
				PortInfo.ReadTimeout = 100;
				PortInfo.WriteTimeout = 100;
				PortInfo.Open();
                rxThread = new Thread(ts);
                rxThread.Start();

                if (OnOpenClose != null) OnOpenClose(true);

                return true;
			}
			catch
			{
				return false;
			}
		}
		public void ClosePort()
		{
			if(PortInfo.IsOpen)
			{
				if(rxThread.IsAlive)
				{
					rxThread.Abort();
                }
                PortInfo.Close();
                if (OnOpenClose != null) OnOpenClose(false);
            }
		}

		public bool SendData(byte[] Msg, MsgFormat Format)
		{
			if(IsOpen)
			{
//				string sendData = Encoding.Default.GetString(Msg);
				PortInfo.Write(Msg, 0, Msg.Length);
				return true;
			}
			else
			{
				return false;
			}
		}

		private void ReadMsg()
		{
			while(PortInfo.IsOpen)
			{
				if(PortInfo.BytesToRead > 0)
				{
					byte[] reads = new byte[PortInfo.BytesToRead];
					PortInfo.Read(reads, 0, PortInfo.BytesToRead);

					if(OnRecvMsg!=null) OnRecvMsg(reads);
				}
				Thread.Sleep(10);
			}
		}

		public bool LoadInit()
		{
			try
			{
				if (File.Exists(".\\comm_info.xml"))
				{
					XElement root = XElement.Load("comm_info.xml");
					XElement uart = root.Element("uart");
					XElement element;

					if (PortInfo == null)
						PortInfo = new SerialPort();

					element = uart.Element("port");
					if(element != null) PortInfo.PortName = element.Value;

					element = uart.Element("baudrate");
					if (element != null) PortInfo.BaudRate = Convert.ToInt32(element.Value);

					element = uart.Element("parity");
					if (element != null) PortInfo.Parity = (Parity)Convert.ToInt32(element.Value);

					element = uart.Element("stopbits");
					if (element != null) PortInfo.StopBits = (StopBits)Convert.ToInt32(element.Value);

					element = uart.Element("databits");
					if (element != null) PortInfo.DataBits = Convert.ToInt32(element.Value);

					return true;
				}
				return false;
			}
			catch
			{
				return false;
			}
		}

		public bool SaveInit()
		{
			if (PortInfo == null)
				return false;

			if (File.Exists(".\\comm_info.xml"))
			{
				XElement root = XElement.Load("comm_info.xml");
				XElement uart = root.Element("uart");
				XElement element;

				if (uart == null)
				{
					uart = new XElement("uart");
					uart.Add(new XElement("port", PortInfo.PortName.ToString()));
					uart.Add(new XElement("baudrate", PortInfo.BaudRate.ToString()));
					uart.Add(new XElement("parity", PortInfo.Parity.ToString()));
					uart.Add(new XElement("stopbits", PortInfo.StopBits.ToString()));
					uart.Add(new XElement("databits", PortInfo.DataBits.ToString()));
					root.Add(uart);
				}
				else
				{
					element = uart.Element("port");
					if (element == null) element.Add(new XElement("port", PortInfo.PortName));
					else if (PortInfo.PortName != null) element.ReplaceWith(new XElement("port", PortInfo.PortName));

					element = uart.Element("baudrate");
					if (element == null) element.Add(new XElement("baudrate", PortInfo.BaudRate.ToString()));
					else element.ReplaceWith(new XElement("baudrate", PortInfo.BaudRate.ToString()));

					element = uart.Element("parity");
					if (element == null) element.Add(new XElement("parity", PortInfo.Parity.ToString()));
					else element.ReplaceWith(new XElement("parity", PortInfo.Parity.ToString()));

					element = uart.Element("stopbits");
					if (element == null) element.Add(new XElement("stopbits", PortInfo.StopBits.ToString()));
					else element.ReplaceWith(new XElement("stopbits", PortInfo.StopBits.ToString()));

					element = uart.Element("databits");
					if (element == null) element.Add(new XElement("databits", PortInfo.DataBits.ToString()));
					else element.ReplaceWith(new XElement("databits", PortInfo.DataBits.ToString()));
				}

				root.Save("comm_info.xml");
			}
			else
			{
				XElement root = new XElement("comm_info");
				XElement uart = new XElement("uart");
				
				uart.Add(new XElement("port", PortInfo.PortName.ToString()));
				uart.Add(new XElement("baudrate", PortInfo.BaudRate.ToString()));
				uart.Add(new XElement("parity", PortInfo.Parity.ToString()));
				uart.Add(new XElement("stopbits", PortInfo.StopBits.ToString()));
				uart.Add(new XElement("databits", PortInfo.DataBits.ToString()));
				root.Add(uart);

				root.Save("comm_info.xml");
			}


			return true;
		}
	}
}
