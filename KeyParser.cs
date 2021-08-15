using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace TouchProtocolTest
{
	static class KeyParser
	{
		public static bool IsHex(char c)
		{
			if(('0' <= c)&&(c <= '9'))
			{
				return true;
			}
			if(('a' <= c) && (c <= 'f'))
			{
				return true;
			}
			if (('A' <= c) && (c <= 'F'))
			{
				return true;
			}
			if(c == ' ')
			{
				return true;
			}
			if(c == '\b')
			{
				return true;
			}
			return false;
		}
		public static byte[] HexStringToHexByte(string input)
		{
			try
			{
				string[] spt = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				List<byte> list = new List<byte>();

				foreach (string item in spt)
				{
					if (item.Length > 2)
					{
						for (int i = 0; i < (item.Length + 1) / 2; i++)
						{
							int length = 2;
							if (item.Length % 2 == 1)
							{
								if (i * 2 == item.Length - 1)
								{
									length = 1;
								}
							}
							string sub = item.Substring(i * 2, length);
							list.Add(Convert.ToByte(sub, 16));
						}
					}
					else
					{
						list.Add(Convert.ToByte(item, 16));
					}
				}

				return list.ToArray();
			}
			catch
			{
				return null;
			}
		}
		public static byte[] AsciiStringToHexByte(string input)
		{
			try
			{
				string AsciiString = Regex.Unescape(input);
				byte[] Msg = Encoding.Default.GetBytes(AsciiString);

				return Msg;
			}
			catch
			{
				return null;
			}
		}
		public static string AnyByteToHexString(byte[] input)
		{
			string msg = "";
			foreach (byte item in input)
			{
				msg += item.ToString("X2") + " ";
			}

			if (msg.Length == 0)
				return null;
			else
				return msg;
		}
		public static string AnyByteToAsciiString(byte[] input)
		{
			string msg = "";
			foreach (byte item in input)
			{
				char character = Convert.ToChar(item);
				msg += ToEscapedString(character);
			}

			if (msg.Length == 0)
				return null;
			else
				return msg;
		}
		public static string AsciiStringToHexString(string Ascii)
		{
			string text = Regex.Unescape(Ascii);
			string toHex = "";
			foreach (char item in text)
			{
				byte integer = Convert.ToByte(item);
				toHex += integer.ToString("X2") + " ";
			}

			return toHex;
		}
		public static string HexStringToAsciiString(string Hex)
		{
			string[] chars = Hex.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			string toAscii = "";
			foreach (string item in chars)
			{
				int integer = Convert.ToInt32(item, 16);
				char character = Convert.ToChar(integer);
				toAscii += ToEscapedString(character);
			}
			return toAscii;
		}
		public static string ToEscapedString(char input)
		{
			switch(input)
			{
				case '\0': return "\\n";
				case '\a': return "\\a";
				case '\b': return "\\b";
				case '\f': return "\\f";
				case '\n': return "\\n";
				case '\t': return "\\t";
				case '\v': return "\\v";
				default:
					{
						if (input < ' ')
							return Regex.Escape(input.ToString());
						else
							return input.ToString();
					}
			}
		}
	}
}
