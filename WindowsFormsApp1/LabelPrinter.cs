using System;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GodexIndustrial
{
    public sealed class LabelPrinter
    {
        private readonly SemaphoreSlim _operations = new SemaphoreSlim(1, 1);
        public string[] BaudRateValues = { "9600", "14400", "19200", "38400", "56000", "57600", "115200", "128000", "230400", "256000", "460800", "921600" };
        public string[] EnumComPorts => SerialPort.GetPortNames();
        public int ConnType { get; set; } = 1;
        private string _ipAddr;
        public string IpAddr
        {
            get => _ipAddr;
            set
            {
                System.Net.IPAddress address;
                if (!System.Net.IPAddress.TryParse(value, out address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    throw new ArgumentException("Enter a valid IPv4 address.");
                _ipAddr = value;
            }
        }
        private int _port = 9100;
        public int Port
        {
            get => _port;
            set
            {
                if (value < 1 || value > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
                _port = value;
            }
        }
        public string ComPortName { get; set; }
        private int _baudRate = 9600;
        public int BaudRate
        {
            get => _baudRate;
            set
            {
                if (!BaudRateValues.Contains(value.ToString())) throw new ArgumentOutOfRangeException(nameof(BaudRate));
                _baudRate = value;
            }
        }
        public string PrinterName { get; set; }

        public void ValidateConnection()
        {
            switch (ConnType)
            {
                case 1:
                    if (string.IsNullOrWhiteSpace(IpAddr)) throw new InvalidOperationException("LAN address is not set.");
                    break;
                case 2:
                    if (string.IsNullOrWhiteSpace(ComPortName)) throw new InvalidOperationException("Select a COM port.");
                    break;
                case 3:
                    if (string.IsNullOrWhiteSpace(PrinterName)) throw new InvalidOperationException("Select a USB printer.");
                    break;
                default:
                    throw new InvalidOperationException("Select a connection type.");
            }
        }

        public async Task PrintAsync(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) throw new ArgumentException("There is no print data.");
            await SendAsync(data);
        }

        public Task CalibrateAsync() => SendAsync("~S,SENSOR" + Environment.NewLine);

        private async Task SendAsync(string data)
        {
            await _operations.WaitAsync();
            try
            {
                ValidateConnection();
                int connectionType = ConnType;
                string ip = IpAddr, com = ComPortName, printer = PrinterName;
                int port = Port, baud = BaudRate;
                await Task.Run(() => Send(data, connectionType, ip, port, com, baud, printer));
            }
            finally
            {
                _operations.Release();
            }
        }

        private static void Send(string data, int type, string ip, int port, string com, int baud, string printer)
        {
            byte[] bytes = Encode(data);
            switch (type)
            {
                case 1:
                    using (var client = Connect(ip, port))
                    using (var stream = client.GetStream())
                    {
                        stream.WriteTimeout = 3000;
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                    }
                    break;
                case 2:
                    using (var serial = new SerialPort(com, baud, Parity.None, 8, StopBits.One))
                    {
                        serial.Handshake = Handshake.None;
                        serial.WriteTimeout = 3000;
                        serial.Open();
                        serial.Write(data);
                    }
                    break;
                case 3:
                    RawPrinterHelper.SendBytesToPrinter(printer, bytes);
                    break;
            }
        }

        private static byte[] Encode(string data)
        {
            // The current printer command configuration uses single-byte ASCII data.
            if (data.Any(c => c > 127))
                throw new ArgumentException("Print data contains non-ASCII characters. This printer profile supports ASCII only.");
            return Encoding.ASCII.GetBytes(data);
        }

        private static TcpClient Connect(string ip, int port)
        {
            var client = new TcpClient();
            try
            {
                IAsyncResult result = client.BeginConnect(ip, port, null, null);
                using (result.AsyncWaitHandle)
                {
                    if (!result.AsyncWaitHandle.WaitOne(3000))
                        throw new TimeoutException("LAN connection timed out.");
                    client.EndConnect(result);
                }
                client.SendTimeout = 3000;
                client.ReceiveTimeout = 3000;
                return client;
            }
            catch
            {
                client.Close();
                throw;
            }
        }

        public async Task<string> QueryStatusAsync()
        {
            await _operations.WaitAsync();
            try
            {
                ValidateConnection();
                int type = ConnType;
                string ip = IpAddr, com = ComPortName;
                int port = Port, baud = BaudRate;
                return await Task.Run(() =>
                {
                    if (type == 3) return "USB status is unavailable";
                    if (type == 1)
                    {
                        using (var client = Connect(ip, port))
                        using (var stream = client.GetStream())
                        {
                            stream.ReadTimeout = 3000;
                            stream.WriteTimeout = 3000;
                            byte[] command = Encoding.ASCII.GetBytes("~S,CHECK" + Environment.NewLine);
                            stream.Write(command, 0, command.Length);
                            byte[] buffer = new byte[1024];
                            int read = stream.Read(buffer, 0, buffer.Length);
                            if (read == 0) throw new IOException("Printer closed the connection without a status reply.");
                            return Encoding.ASCII.GetString(buffer, 0, read).Trim();
                        }
                    }
                    using (var serial = new SerialPort(com, baud, Parity.None, 8, StopBits.One))
                    {
                        serial.ReadTimeout = 3000;
                        serial.WriteTimeout = 3000;
                        serial.Open();
                        serial.WriteLine("~S,CHECK");
                        return serial.ReadLine().Trim();
                    }
                });
            }
            finally
            {
                _operations.Release();
            }
        }
    }

    internal static class RawPrinterHelper
    {
        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);
        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr handle);
        [DllImport("winspool.drv", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern bool StartDocPrinter(IntPtr handle, int level, [In] ref DocInfo info);
        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr handle);
        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr handle);
        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr handle);
        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr handle, IntPtr bytes, int count, out int written);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DocInfo
        {
            [MarshalAs(UnmanagedType.LPStr)] public string Name;
            [MarshalAs(UnmanagedType.LPStr)] public string OutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string DataType;
        }

        public static void SendBytesToPrinter(string printer, byte[] data)
        {
            IntPtr handle = IntPtr.Zero;
            IntPtr buffer = IntPtr.Zero;
            bool documentStarted = false, pageStarted = false;
            try
            {
                if (!OpenPrinter(printer, out handle, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot open printer.");
                var info = new DocInfo { Name = "Label Print", DataType = "RAW" };
                if (!StartDocPrinter(handle, 1, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot start print job.");
                documentStarted = true;
                if (!StartPagePrinter(handle)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot start print page.");
                pageStarted = true;
                buffer = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, buffer, data.Length);
                int written;
                if (!WritePrinter(handle, buffer, data.Length, out written))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot write print data.");
                if (written != data.Length)
                    throw new IOException($"Printer accepted only {written} of {data.Length} bytes.");
                if (!EndPagePrinter(handle)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot finish print page.");
                pageStarted = false;
                if (!EndDocPrinter(handle)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot finish print job.");
                documentStarted = false;
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeCoTaskMem(buffer);
                if (pageStarted) EndPagePrinter(handle);
                if (documentStarted) EndDocPrinter(handle);
                if (handle != IntPtr.Zero) ClosePrinter(handle);
            }
        }
    }
}
