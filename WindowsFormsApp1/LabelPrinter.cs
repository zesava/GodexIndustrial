using System;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Globalization;
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

        public async Task<string> UploadFontAsync(byte[] data, string fontName, char slot,
            IProgress<int> progress = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < 12 ||
                !((data[0] == 0 && data[1] == 1 && data[2] == 0 && data[3] == 0) ||
                  (data[0] == 't' && data[1] == 'r' && data[2] == 'u' && data[3] == 'e')))
                throw new InvalidDataException("The selected font is not a supported TrueType font.");
            if (slot < 'A' || slot > 'Z')
                throw new ArgumentOutOfRangeException(nameof(slot), "Font slot must be A–Z.");

            string name = new string((fontName ?? string.Empty)
                .Where(character => (character >= 'A' && character <= 'Z') ||
                                    (character >= 'a' && character <= 'z') ||
                                    (character >= '0' && character <= '9')).ToArray());
            if (name.Length == 0) name = "Font" + slot;

            await _operations.WaitAsync();
            try
            {
                ValidateConnection();
                int type = ConnType;
                if (type == 3)
                    throw new NotSupportedException(
                        "Font upload through the Windows USB print queue is unavailable. Use LAN or COM.");
                string ip = IpAddr, com = ComPortName;
                int port = Port, baud = BaudRate;
                return await Task.Run(() =>
                {
                    byte[] header = Encoding.ASCII.GetBytes("~H,TTF," + slot + name + "," +
                        data.Length.ToString(CultureInfo.InvariantCulture) + "\r");
                    using (var font = new MemoryStream(data, false))
                        SendFont(header, font, type, ip, port, com, baud, progress);
                    return slot + ": " + name;
                });
            }
            finally
            {
                _operations.Release();
            }
        }
        private static void SendFont(byte[] header, Stream font, int type,
            string ip, int port, string com, int baud, IProgress<int> progress)
        {
            if (type == 1)
            {
                using (var client = Connect(ip, port))
                using (var stream = client.GetStream())
                {
                    stream.WriteTimeout = 5000;
                    WriteFontBytes((bytes, offset, count) =>
                        stream.Write(bytes, offset, count), header, font, 16 * 1024, progress);
                    stream.Flush();
                }
            }
            else
            {
                using (var serial = new SerialPort(com, baud, Parity.None, 8, StopBits.One))
                {
                    serial.Handshake = Handshake.None;
                    serial.WriteTimeout = 5000;
                    serial.Open();
                    WriteFontBytes((bytes, offset, count) =>
                        serial.Write(bytes, offset, count), header, font, 512, progress);
                }
            }
        }
        private static void WriteFontBytes(Action<byte[], int, int> write,
            byte[] header, Stream font, int chunkSize, IProgress<int> progress)
        {
            write(header, 0, header.Length);
            byte[] buffer = new byte[chunkSize];
            long sent = 0;
            int reported = -1;
            int count;
            while ((count = font.Read(buffer, 0, buffer.Length)) > 0)
            {
                write(buffer, 0, count);
                sent += count;
                int percent = (int)(sent * 100 / font.Length);
                if (percent != reported)
                {
                    progress?.Report(percent);
                    reported = percent;
                }
            }
        }

        public async Task DeleteFontAsync(PrinterFontInfo font)
        {
            if (font == null) throw new ArgumentNullException(nameof(font));
            if (!font.TryBuildDeleteCommand(out string command))
                throw new ArgumentException("The selected font has no valid printer ID.", nameof(font));

            await _operations.WaitAsync();
            try
            {
                ValidateConnection();
                int type = ConnType;
                if (type == 3)
                    throw new NotSupportedException(
                        "Font deletion through the Windows USB print queue is unavailable. Use LAN or COM.");
                string ip = IpAddr, com = ComPortName;
                int port = Port, baud = BaudRate;
                await Task.Run(() => Send(command + Environment.NewLine,
                    type, ip, port, com, baud, null));
            }
            finally
            {
                _operations.Release();
            }
        }

        public async Task<PrinterFontCatalog> QueryPrinterFontsAsync()
        {
            await _operations.WaitAsync();
            try
            {
                ValidateConnection();
                int type = ConnType;
                if (type == 3)
                    throw new NotSupportedException(
                        "The Windows USB print queue cannot read printer font listings. Use LAN or COM.");
                string ip = IpAddr, com = ComPortName;
                int port = Port, baud = BaudRate;
                return await Task.Run(() =>
                    PrinterFontCatalog.ParseDirectory(
                        QueryMemoryDirectory(type, ip, port, com, baud)));
            }
            finally
            {
                _operations.Release();
            }
        }

        private static string QueryMemoryDirectory(int type, string ip, int port,
            string com, int baud)
        {
            const string command = "~MDIR";
            if (type == 1)
            {
                using (var client = Connect(ip, port))
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 750;
                    stream.WriteTimeout = 3000;
                    byte[] bytes = Encoding.ASCII.GetBytes(command + Environment.NewLine);
                    stream.Write(bytes, 0, bytes.Length);
                    return ReadMemoryDirectory((buffer, offset, count) =>
                        stream.Read(buffer, offset, count));
                }
            }

            using (var serial = new SerialPort(com, baud, Parity.None, 8, StopBits.One))
            {
                serial.Handshake = Handshake.None;
                serial.ReadTimeout = 750;
                serial.WriteTimeout = 3000;
                serial.Open();
                serial.WriteLine(command);
                return ReadMemoryDirectory((buffer, offset, count) =>
                    serial.Read(buffer, offset, count));
            }
        }

        private static string ReadMemoryDirectory(Func<byte[], int, int, int> read)
        {
            var reply = new StringBuilder();
            var buffer = new byte[4096];
            DateTime deadline = DateTime.UtcNow.AddSeconds(6);
            DateTime lastByte = DateTime.UtcNow;
            while (DateTime.UtcNow < deadline && reply.Length < 65536)
            {
                try
                {
                    int count = read(buffer, 0, buffer.Length);
                    if (count == 0) break;
                    reply.Append(Encoding.ASCII.GetString(buffer, 0, count));
                    lastByte = DateTime.UtcNow;
                }
                catch (TimeoutException)
                {
                    if (reply.Length > 0 && (DateTime.UtcNow - lastByte).TotalMilliseconds >= 1500)
                        break;
                }
                catch (IOException ex)
                {
                    var socketError = ex.InnerException as SocketException;
                    if (socketError == null || socketError.SocketErrorCode != SocketError.TimedOut)
                        throw;
                    if (reply.Length > 0 && (DateTime.UtcNow - lastByte).TotalMilliseconds >= 1500)
                        break;
                }
            }

            if (reply.Length == 0)
                throw new TimeoutException("Printer did not respond to the memory directory request.");
            return reply.ToString();
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
                            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                            {
                                string response = reader.ReadLine();
                                if (response == null)
                                    throw new IOException("Printer closed the connection without a status reply.");
                                return response.Trim();
                            }
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
