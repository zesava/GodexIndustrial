using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GodexIndustrial;

namespace GodexIndustrial.Tests
{
    internal static class Program
    {
        private static int _passed;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            _passed++;
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { _passed++; return; }
            throw new Exception("Expected " + typeof(T).Name);
        }

        private static LabelTemplate Template()
        {
            return new LabelTemplate
            {
                Name = "Sample", ColumnCount = 2, XOffsets = new List<int> { 20, 120 },
                YOffset = 10, FontSize = 27, LabelWidth = 40, LabelLength = 6,
                LabelGap = 3, Darkness = 12, Rotation = 0, PrintSpeed = 2
            };
        }

        private static void VerifyFontUpload(System.Reflection.Assembly application,
            byte[] payload, string expectedHeader, string expectedName)
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var received = System.Threading.Tasks.Task.Run(() =>
                {
                    using (var client = listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    using (var memory = new MemoryStream())
                    {
                        stream.ReadTimeout = 5000;
                        stream.CopyTo(memory);
                        return memory.ToArray();
                    }
                });
                Type printerType = application.GetType("GodexIndustrial.LabelPrinter");
                object printer = Activator.CreateInstance(printerType);
                printerType.GetProperty("IpAddr").SetValue(printer, "127.0.0.1");
                printerType.GetProperty("Port").SetValue(printer,
                    ((System.Net.IPEndPoint)listener.LocalEndpoint).Port);
                var upload = System.Threading.Tasks.Task.Run(() =>
                    ((System.Threading.Tasks.Task<string>)printerType
                        .GetMethod("UploadFontAsync", new[]
                        {
                            typeof(byte[]), typeof(string), typeof(char), typeof(IProgress<int>)
                        })
                        .Invoke(printer, new object[] { payload, "Test Font", 'B', null }))
                        .GetAwaiter().GetResult());
                Check(upload.GetAwaiter().GetResult() == expectedName,
                    "Windows font upload reports its printer font slot");
                byte[] command = System.Text.Encoding.ASCII.GetBytes(expectedHeader);
                byte[] expected = command.Concat(payload).ToArray();
                Check(received.GetAwaiter().GetResult().SequenceEqual(expected),
                    "Windows font upload sends the exact header and unmodified binary bytes");
            }
            finally
            {
                listener.Stop();
            }
        }

        private static void VerifyFontDeletion(System.Reflection.Assembly application,
            string type, string name, string expectedCommand)
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var received = System.Threading.Tasks.Task.Run(() =>
                {
                    using (var client = listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    using (var memory = new MemoryStream())
                    {
                        stream.ReadTimeout = 5000;
                        stream.CopyTo(memory);
                        return memory.ToArray();
                    }
                });
                Type printerType = application.GetType("GodexIndustrial.LabelPrinter");
                Type fontType = application.GetType("GodexIndustrial.PrinterFontInfo");
                object font = Activator.CreateInstance(fontType, type, name, "");
                object printer = Activator.CreateInstance(printerType);
                printerType.GetProperty("IpAddr").SetValue(printer, "127.0.0.1");
                printerType.GetProperty("Port").SetValue(printer,
                    ((System.Net.IPEndPoint)listener.LocalEndpoint).Port);
                var deletion = System.Threading.Tasks.Task.Run(() =>
                    ((System.Threading.Tasks.Task)printerType.GetMethod("DeleteFontAsync")
                        .Invoke(printer, new[] { font })).GetAwaiter().GetResult());
                Check(deletion.Wait(TimeSpan.FromSeconds(10)),
                    type + " deletion completes without waiting on the UI thread");
                Check(received.Wait(TimeSpan.FromSeconds(10)),
                    type + " deletion reaches the TCP printer");
                Check(received.GetAwaiter().GetResult().SequenceEqual(
                    System.Text.Encoding.ASCII.GetBytes(expectedCommand + Environment.NewLine)),
                    type + " deletion sends only the selected font command");
            }
            finally
            {
                listener.Stop();
            }
        }
        [STAThread]
        private static void Main()
        {
            Check(PrinterStatusInfo.Parse("00\r\n").Kind == PrinterStatusKind.Ready,
                "GoDEX ready status");
            Check(PrinterStatusInfo.Parse("04\r\n").Description == "Printhead open",
                "GoDEX open printhead status");
            Check(PrinterStatusInfo.Parse("20").Kind == PrinterStatusKind.Busy,
                "GoDEX paused status");
            Check(PrinterStatusInfo.Parse("50").Description == "Printing",
                "GoDEX printing status");
            Check(PrinterStatusInfo.Parse("99").Kind == PrinterStatusKind.Unknown,
                "Unknown GoDEX status code");
            Check(PrinterStatusInfo.Parse("").Kind == PrinterStatusKind.Unknown,
                "Empty GoDEX status response");
            var rows = LabelDataParser.ParseTsv("A\tB\r\nC\t\r\n", 2);
            Check(rows.Count == 2 && rows[0][1] == "B" && rows[1][1] == "", "Windows TSV parse");
            rows = LabelDataParser.ParseTsv("A\tB\nC\tD", 2);
            Check(rows.Count == 2 && rows[1][1] == "D", "Unix TSV parse");
            Throws<ArgumentException>(() => LabelDataParser.ParseTsv("A\tB\tC", 2));
            Throws<ArgumentException>(() => LabelDataParser.ParseTsv("", 2));

            var template = Template();
            string commands = LabelCommandBuilder.Build(template,
                new[] { new[] { "A", "B" }, new[] { "C", "" } });
            Check(commands.Contains("ATA,20,10,27,27,0,0BE,A,0,A"), "First column command");
            Check(commands.Contains("ATA,120,10,27,27,0,0BE,A,0,B"), "Second column command");
            Check(commands.Split(new[] { Environment.NewLine + "E" + Environment.NewLine }, StringSplitOptions.None).Length == 3, "Two label terminators");
            Throws<ArgumentException>(() => LabelCommandBuilder.Build(template, new[] { new[] { "^L", "B" } }));
            Throws<ArgumentException>(() => LabelCommandBuilder.Build(template, new string[0][]));
            for (int rotation = 0; rotation < 4; rotation++)
            {
                template.Rotation = rotation;
                string rotatedCommand = LabelCommandBuilder.Build(template, new[] { new[] { "X", "Y" } });
                Check(rotatedCommand.Contains($"ATA,20,10,27,27,0,{rotation}BE,A,0,X"),
                    $"Rotation {rotation} reaches the first print field");
                Check(rotatedCommand.Contains($"ATA,120,10,27,27,0,{rotation}BE,A,0,Y"),
                    $"Rotation {rotation} reaches the second print field");
                Check(LabelRotation.Degrees(rotation) == rotation * 90,
                    $"Rotation {rotation} has the correct angle");
                string json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(template);
                Check(new System.Web.Script.Serialization.JavaScriptSerializer()
                    .Deserialize<LabelTemplate>(json).Rotation == rotation,
                    $"Rotation {rotation} survives template serialization");
            }
            template.Rotation = 0;
            var selectedTtf = new PrinterFontInfo("TTF", "B: Arial", "");
            string ttfCommands = LabelCommandBuilder.Build(template,
                new[] { new[] { "X", "Y" } }, selectedTtf);
            Check(ttfCommands.Contains("ATB,20,10,27,27,0,0BE,A,0,X") &&
                ttfCommands.Contains("ATB,120,10,27,27,0,0BE,A,0,Y"),
                "Selected TTF slot is used for each print field");
            template.Rotation = 2;
            string fntCommands = LabelCommandBuilder.Build(template,
                new[] { new[] { "X", "Y" } },
                new PrinterFontInfo("FNT", "A.FNT", ""));
            Check(fntCommands.Contains("VA,20,10,1,1,0,2,X") &&
                fntCommands.Contains("VA,120,10,1,1,0,2,Y"),
                "Selected FNT uses bitmap command with rotation");
            Throws<ArgumentException>(() => LabelCommandBuilder.Build(template,
                new[] { new[] { "X", "Y" } },
                new PrinterFontInfo("TTF", "No printer ID", "")));
            template.Rotation = 0;
            var zero = LabelRotation.RotatedBounds(10, 20, 30, 5, 0);
            var ninety = LabelRotation.RotatedBounds(10, 20, 30, 5, 1);
            var oneEighty = LabelRotation.RotatedBounds(10, 20, 30, 5, 2);
            var twoSeventy = LabelRotation.RotatedBounds(10, 20, 30, 5, 3);
            Check(zero.Left == 10 && zero.Top == 20 && zero.Right == 40 && zero.Bottom == 25,
                "Preview bounds at 0 degrees");
            Check(ninety.Left == 5 && ninety.Top == 20 && ninety.Right == 10 && ninety.Bottom == 50,
                "Preview bounds at 90 degrees");
            Check(oneEighty.Left == -20 && oneEighty.Top == 15 && oneEighty.Right == 10 && oneEighty.Bottom == 20,
                "Preview bounds at 180 degrees");
            Check(twoSeventy.Left == 10 && twoSeventy.Top == -10 && twoSeventy.Right == 15 && twoSeventy.Bottom == 20,
                "Preview bounds at 270 degrees");
            Throws<ArgumentOutOfRangeException>(() => LabelRotation.Degrees(4));
            template = Template();
            System.Drawing.SizeF labelDots = PreviewGeometry.LabelSizeDots(template, 203);
            Check(labelDots.Width == 320 && labelDots.Height == 48,
                "203 dpi frame converts 40 × 6 mm to dots");
            Check(PreviewGeometry.GapDots(template, 203) == 24,
                "203 dpi gap converts 3 mm to dots");
            Check(PreviewGeometry.FitsOnLabel(template, 203,
                new System.Drawing.RectangleF(20, 10, 30, 20)), "Text inside the label");
            Check(!PreviewGeometry.FitsOnLabel(template, 203,
                new System.Drawing.RectangleF(20, 50, 30, 20)), "Text beyond the label is detected");
            Throws<ArgumentOutOfRangeException>(() => PreviewGeometry.DotsPerMillimeter(600));

            template.Name = "../escape";
            Throws<ArgumentException>(() => TemplateManager.ValidateTemplate(template));
            template = Template();
            template.XOffsets.Clear();
            Throws<ArgumentException>(() => TemplateManager.ValidateTemplate(template));

            string directory = Path.Combine(Path.GetTempPath(), "godex-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "value.txt");
            try
            {
                AppStorage.WriteAtomically(path, "first");
                AppStorage.WriteAtomically(path, "second");
                Check(File.ReadAllText(path) == "second", "Atomic replacement");
                Check(Directory.GetFiles(directory).Length == 1, "Temporary file cleanup");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                Directory.Delete(directory);
            }
            Check(AppStorage.DirectoryPath == AppDomain.CurrentDomain.BaseDirectory,
                "Settings, templates and logs are rooted beside the executable");

            using (var printers = new System.Windows.Forms.ComboBox())
            {
                printers.Items.Add("Printer A");
                printers.Items.Add("Printer B");
                Check(PrinterSelection.Choose(printers.Items, null, null) == "Printer A",
                    "Missing saved and default printer uses first available");
                Check(PrinterSelection.Choose(printers.Items, null, "Printer B") == "Printer B",
                    "Missing saved printer uses installed default");
                Check(PrinterSelection.Choose(printers.Items, "Printer A", "Printer B") == "Printer A",
                    "Saved printer takes priority");
                printers.Items.Clear();
                Check(PrinterSelection.Choose(printers.Items, null, null) == null,
                    "No printers remains unselected");
            }
            string configuration = Path.GetFileName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
            string appDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "WindowsFormsApp1", "bin", configuration));
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string name = new System.Reflection.AssemblyName(args.Name).Name;
                string dependency = Path.Combine(appDirectory, name + ".dll");
                return File.Exists(dependency) ? System.Reflection.Assembly.LoadFrom(dependency) : null;
            };
            var application = System.Reflection.Assembly.LoadFrom(Path.Combine(appDirectory, "GodexIndustrial.exe"));
            using (var selectedFont = new System.Drawing.Font(
                System.Drawing.FontFamily.GenericSansSerif, 12F))
            {
                Type fontReader = application.GetType("GodexIndustrial.InstalledFontData");
                byte[] installedBytes = (byte[])fontReader
                    .GetMethod("ReadTrueType",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] { selectedFont });
                Check(installedBytes.Length > 12 &&
                    installedBytes[0] == 0 && installedBytes[1] == 1 &&
                    installedBytes[2] == 0 && installedBytes[3] == 0,
                    "Selected Windows font exposes TrueType bytes");
            }
            var parsedFonts = PrinterFontCatalog.ParseDirectory(
                "FLASH MEMORY\r\nLabel1 LBL\r\nA FNT\r\n" +
                "A: CP850_Latin1 TTF_TABLE\r\nA: Arial (True Type) TTF\r\n" +
                "559104 byte(s) free\r\n");
            Check(parsedFonts.Fonts.Count == 2 &&
                parsedFonts.Fonts[0].Type == "FNT" && parsedFonts.Fonts[0].Name == "A.FNT" &&
                parsedFonts.Fonts[1].Type == "TTF" && parsedFonts.Fonts[1].Name == "A: Arial",
                "~MDIR catalog parses FNT and TTF, excluding the TTF table");
            Check(parsedFonts.FreeMemoryKb == "546" &&
                parsedFonts.Fonts[0].Size == string.Empty &&
                parsedFonts.Fonts[1].Size == string.Empty,
                "Directory reports free flash memory and does not invent font sizes");
            Check(parsedFonts.FindAvailableSlot("TTF") == 'B' &&
                parsedFonts.FindAvailableSlot("FNT") == 'B',
                "Font upload chooses a free slot for each font type");
            Check(new PrinterFontInfo("TTF", "B: Arial", "")
                    .TryBuildDeleteCommand(out string ttfDelete) &&
                ttfDelete == "~MDELC,B",
                "TTF deletion uses the printer font ID");
            Check(new PrinterFontInfo("FNT", "A.FNT", "")
                    .TryBuildDeleteCommand(out string fntDelete) &&
                fntDelete == "~MDELE,A",
                "Bitmap font deletion uses its one-letter name");
            Check(!new PrinterFontInfo("TTF", "Arial", "")
                    .TryBuildDeleteCommand(out _) &&
                !new PrinterFontInfo("FNT", "*.FNT", "")
                    .TryBuildDeleteCommand(out _),
                "Ambiguous font rows cannot form delete commands");
            Type deletePrinterType = application.GetType("GodexIndustrial.LabelPrinter");
            Type deleteFontType = application.GetType("GodexIndustrial.PrinterFontInfo");
            object usbPrinter = Activator.CreateInstance(deletePrinterType);
            deletePrinterType.GetProperty("ConnType").SetValue(usbPrinter, 3);
            deletePrinterType.GetProperty("PrinterName").SetValue(usbPrinter, "Test queue");
            object usbFont = Activator.CreateInstance(deleteFontType, "TTF", "B: Arial", "");
            Throws<NotSupportedException>(() =>
                System.Threading.Tasks.Task.Run(() =>
                    ((System.Threading.Tasks.Task)deletePrinterType.GetMethod("DeleteFontAsync")
                        .Invoke(usbPrinter, new[] { usbFont })).GetAwaiter().GetResult())
                    .GetAwaiter().GetResult());
            VerifyFontDeletion(application, "TTF", "B: Arial", "~MDELC,B");
            VerifyFontDeletion(application, "FNT", "A.FNT", "~MDELE,A");
            VerifyFontUpload(application,
                new byte[] { 0, 1, 0, 0, 0, 1, 0xFF, 0, 0x1B, 0, 0, 0 },
                "~H,TTF,BTestFont,12\r", "B: TestFont");
            string fullDirectory = "FLASH MEMORY\r\n" +
                string.Concat(Enumerable.Range(0, 26)
                    .Select(index => ((char)('A' + index)) + ": Font" + index + " TTF\r\n"));
            Throws<InvalidOperationException>(() =>
                PrinterFontCatalog.ParseDirectory(fullDirectory).FindAvailableSlot("TTF"));

            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var statusReply = System.Threading.Tasks.Task.Run(() =>
                {
                    using (var client = listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    {
                        stream.ReadTimeout = 3000;
                        var received = new System.Text.StringBuilder();
                        var buffer = new byte[64];
                        while (!received.ToString().Contains("~S,CHECK\r\n"))
                        {
                            int count = stream.Read(buffer, 0, buffer.Length);
                            if (count == 0) throw new IOException("Status query ended early.");
                            received.Append(System.Text.Encoding.ASCII.GetString(buffer, 0, count));
                        }
                        byte[] first = System.Text.Encoding.ASCII.GetBytes("0");
                        stream.Write(first, 0, first.Length);
                        System.Threading.Tasks.Task.Delay(20).Wait();
                        byte[] rest = System.Text.Encoding.ASCII.GetBytes("4\r\n");
                        stream.Write(rest, 0, rest.Length);
                        return received.ToString();
                    }
                });
                Type printerType = application.GetType("GodexIndustrial.LabelPrinter");
                object printer = Activator.CreateInstance(printerType);
                printerType.GetProperty("IpAddr").SetValue(printer, "127.0.0.1");
                printerType.GetProperty("Port").SetValue(printer,
                    ((System.Net.IPEndPoint)listener.LocalEndpoint).Port);
                var query = System.Threading.Tasks.Task.Run(() =>
                    ((System.Threading.Tasks.Task<string>)printerType
                        .GetMethod("QueryStatusAsync").Invoke(printer, null))
                        .GetAwaiter().GetResult());
                Check(query.GetAwaiter().GetResult() == "04", "Split TCP status reply is read completely");
                string sent = statusReply.GetAwaiter().GetResult();
                Check(sent == "~S,CHECK" + Environment.NewLine,
                    "Status query sends only the GoDEX check command");
            }
            finally
            {
                listener.Stop();
            }
            var fontListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            fontListener.Start();
            try
            {
                var fontCommand = System.Threading.Tasks.Task.Run(() =>
                {
                    using (var client = fontListener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    {
                        var bytes = new List<byte>();
                        while (bytes.Count < 32)
                        {
                            int value = stream.ReadByte();
                            if (value < 0) throw new IOException("Directory query ended early.");
                            bytes.Add((byte)value);
                            if (value == '\n') break;
                        }
                        byte[] response = System.Text.Encoding.ASCII.GetBytes(
                            "FLASH MEMORY\r\nA FNT\r\nA: Arial (True Type) TTF\r\n" +
                            "559104 byte(s) free\r\n");
                        stream.Write(response, 0, response.Length);
                        return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
                    }
                });
                Type printerType = application.GetType("GodexIndustrial.LabelPrinter");
                object fontPrinter = Activator.CreateInstance(printerType);
                printerType.GetProperty("IpAddr").SetValue(fontPrinter, "127.0.0.1");
                printerType.GetProperty("Port").SetValue(fontPrinter,
                    ((System.Net.IPEndPoint)fontListener.LocalEndpoint).Port);
                var fontQuery = System.Threading.Tasks.Task.Run(() =>
                {
                    var task = (System.Threading.Tasks.Task)printerType
                        .GetMethod("QueryPrinterFontsAsync").Invoke(fontPrinter, null);
                    task.GetAwaiter().GetResult();
                    return task.GetType().GetProperty("Result").GetValue(task);
                });
                object catalog = fontQuery.GetAwaiter().GetResult();
                var fontRows = (System.Collections.ICollection)catalog.GetType()
                    .GetProperty("Fonts").GetValue(catalog);
                Check(fontRows.Count == 2, "LAN directory query returns printer fonts");
                Check(fontCommand.GetAwaiter().GetResult() == "~MDIR" + Environment.NewLine,
                    "Font query sends only the memory directory command");
            }
            finally
            {
                fontListener.Stop();
            }
            using (var form = (IDisposable)Activator.CreateInstance(application.GetType("GodexIndustrial.Form1")))
            {
                Check(form != null, "Main form opens without a saved printer");
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var sidebar = (System.Windows.Forms.Panel)form.GetType()
                    .GetField("panelMenu", flags).GetValue(form);
                sidebar.PerformLayout();
                var fontsButton = (System.Windows.Forms.Control)form.GetType()
                    .GetField("iconFonts", flags).GetValue(form);
                var connectionButton = (System.Windows.Forms.Control)form.GetType()
                    .GetField("iconConnection", flags).GetValue(form);
                Check(fontsButton.Top == connectionButton.Bottom,
                    "Fonts button is directly below Printer connection");
                var fontsList = (System.Windows.Forms.ListView)form.GetType()
                    .GetField("_fontsList", flags).GetValue(form);
                Check(fontsList.Columns.Count == 3 && fontsList.Groups.Count == 2 &&
                    fontsList.Groups["FNT"] != null && fontsList.Groups["TTF"] != null,
                    "Fonts tab has type, file name, size and FNT/TTF groups");
                var fontsTab = (System.Windows.Forms.TabPage)form.GetType()
                    .GetField("tabFonts", flags).GetValue(form);
                var fontsLayout = (System.Windows.Forms.TableLayoutPanel)fontsTab.Controls[0];
                fontsLayout.PerformLayout();
                var fontsHeader = (System.Windows.Forms.TableLayoutPanel)
                    fontsLayout.GetControlFromPosition(0, 0);
                fontsHeader.PerformLayout();
                var refreshFontsButton = (System.Windows.Forms.Button)form.GetType()
                    .GetField("_refreshFontsButton", flags).GetValue(form);
                var uploadFontButton = (System.Windows.Forms.Button)form.GetType()
                    .GetField("_uploadFontButton", flags).GetValue(form);
                var deleteFontButton = (System.Windows.Forms.Button)form.GetType()
                    .GetField("_deleteFontButton", flags).GetValue(form);
                var fontStatus = (System.Windows.Forms.Label)form.GetType()
                    .GetField("_fontsStatus", flags).GetValue(form);
                string prompt = fontStatus.Text;

                // Showing the form creates the native TabControl handle. An inserted
                // page used to disappear at this point, leaving the entire tab blank.
                object savedSettings = form.GetType().GetField("_settings", flags).GetValue(form);
                savedSettings.GetType().GetProperty("ConnectionType").SetValue(savedSettings, 3);
                var window = (System.Windows.Forms.Form)form;
                window.ShowInTaskbar = false;
                window.Opacity = 0;
                window.Show();
                System.Windows.Forms.Application.DoEvents();
                form.GetType().GetMethod("iconFonts_Click", flags)
                    .Invoke(form, new object[] { fontsButton, EventArgs.Empty });
                System.Windows.Forms.Application.DoEvents();

                var fontsTabControl = (System.Windows.Forms.TabControl)fontsTab.Parent;
                Check(fontsTabControl.TabPages.Cast<System.Windows.Forms.TabPage>()
                    .All(page => page.BackColor == System.Drawing.Color.White) &&
                    ((System.Windows.Forms.DataGridView)form.GetType()
                        .GetField("myDataGridView", flags).GetValue(form))
                        .BackgroundColor == System.Drawing.Color.White,
                    "All page backgrounds and the print-data grid are white");
                Check(new[] { "panelTemplate", "tableLayoutPanel1", "panel1", "panel2",
                        "panelPrint", "groupBox1", "groupBox2", "groupBox3", "groupBox5" }
                    .All(name => ((System.Windows.Forms.Control)form.GetType()
                        .GetField(name, flags).GetValue(form)).BackColor ==
                        System.Drawing.Color.White),
                    "Child content panels inherit the white page background");
                Check(fontsTabControl.TabPages.Contains(fontsTab) &&
                    fontsTabControl.SelectedTab == fontsTab &&
                    fontsTab.Visible && fontsLayout.Visible &&
                    fontsHeader.Visible && refreshFontsButton.Visible &&
                    uploadFontButton.Visible && deleteFontButton.Visible &&
                    fontsList.Visible && fontStatus.Visible,
                    "Fonts tab and its controls remain visible after the form opens");
                Check(refreshFontsButton.Text == "Оновити" &&
                    uploadFontButton.Text == "Завантажити" &&
                    deleteFontButton.Text == "Видалити" && !deleteFontButton.Enabled &&
                    refreshFontsButton.Parent == fontsHeader &&
                    uploadFontButton.Parent == fontsHeader &&
                    deleteFontButton.Parent == fontsHeader &&
                    fontsList.Top >= fontsHeader.Bottom &&
                    refreshFontsButton.Right <= fontsHeader.ClientSize.Width &&
                    fontsLayout.Width > 200 && fontsList.Height > 100,
                    "Refresh button is visible above the font list");
                Check(fontStatus.Text == prompt &&
                    !(bool)form.GetType().GetField("_fontsLoading", flags).GetValue(form),
                    "Opening Fonts does not send a printer query");
                Type appCatalog = application.GetType("GodexIndustrial.PrinterFontCatalog");
                object uiCatalog = appCatalog.GetMethod("ParseDirectory").Invoke(null,
                    new object[] { "FLASH MEMORY\r\nA FNT\r\nB: Arial TTF\r\n" });
                form.GetType().GetMethod("ShowFontCatalog", flags)
                    .Invoke(form, new[] { uiCatalog });
                var printFontList = (System.Windows.Forms.ComboBox)form.GetType()
                    .GetField("cmbPrinterFont", flags).GetValue(form);
                Check(printFontList.Items.Count == 2 &&
                    printFontList.SelectedItem.ToString() == "TTF — B: Arial",
                    "Label font list shows printer catalog and selects TTF by default");
                var printTab = (System.Windows.Forms.TabPage)form.GetType()
                    .GetField("tabPage2", flags).GetValue(form);
                var printPanel = (System.Windows.Forms.Panel)form.GetType()
                    .GetField("panelPrint", flags).GetValue(form);
                var printGrid = (System.Windows.Forms.DataGridView)form.GetType()
                    .GetField("myDataGridView", flags).GetValue(form);
                var fontRefresh = (System.Windows.Forms.Control)form.GetType()
                    .GetField("btnRefreshPrinterFonts", flags).GetValue(form);
                var fontHint = (System.Windows.Forms.Control)form.GetType()
                    .GetField("lblPrinterFontHint", flags).GetValue(form);
                Check(printFontList.Parent == printPanel &&
                    fontRefresh.Parent == printPanel && fontHint.Parent == printPanel,
                    "Printer font controls are in Print data, not Label");
                fontsTabControl.SelectedTab = printTab;
                System.Windows.Forms.Application.DoEvents();
                Check(printPanel.Visible && printFontList.Visible &&
                    fontRefresh.Visible && fontHint.Visible &&
                    printGrid.Top >= printPanel.Bottom && printGrid.Height > 100,
                    "Printer font controls and data grid fit in Print data");
                fontsTabControl.SelectedTab = fontsTab;
                System.Windows.Forms.Application.DoEvents();
                printFontList.SelectedIndex = 0;
                form.GetType().GetMethod("ShowFontCatalog", flags)
                    .Invoke(form, new[] { uiCatalog });
                Check(printFontList.SelectedItem.ToString() == "FNT — A.FNT",
                    "Refreshing the catalog preserves the selected font");
                object withoutSelectedFont = appCatalog.GetMethod("ParseDirectory").Invoke(null,
                    new object[] { "FLASH MEMORY\r\nB: Arial TTF\r\n" });
                form.GetType().GetMethod("ShowFontCatalog", flags)
                    .Invoke(form, new[] { withoutSelectedFont });
                Check(printFontList.SelectedIndex == -1,
                    "A removed font is not silently replaced for printing");
                form.GetType().GetMethod("ShowFontCatalog", flags)
                    .Invoke(form, new[] { uiCatalog });
                printFontList.SelectedIndex = 0;
                Check(!deleteFontButton.Enabled && fontsList.Items.Count == 2,
                    "Delete font starts disabled until a row is selected");
                fontsList.Items[0].Selected = true;
                System.Windows.Forms.Application.DoEvents();
                Check(deleteFontButton.Enabled,
                    "Selecting a font enables its delete button");
                fontsList.Items[0].Selected = false;
                System.Windows.Forms.Application.DoEvents();
                Check(!deleteFontButton.Enabled,
                    "Clearing the font selection disables deletion");
                fontsList.Items[1].Selected = true;
                System.Windows.Forms.Application.DoEvents();
                Check(deleteFontButton.Enabled,
                    "Selecting a TTF font also enables deletion");
                fontsList.Items[1].Selected = false;
                System.Windows.Forms.Application.DoEvents();
                var statusLabel = (System.Windows.Forms.Label)form.GetType()
                    .GetField("lblPrinterStatus", flags).GetValue(form);
                Check(statusLabel.Parent.Name == "panelLogo",
                    "Printer status appears below the app title");
                form.GetType().GetField("_statusMonitoringStarted", flags).SetValue(form, true);
                form.GetType().GetField("_lanAddressApplied", flags).SetValue(form, false);
                object uiPrinter = form.GetType().GetField("_printer", flags).GetValue(form);
                uiPrinter.GetType().GetProperty("ConnType").SetValue(uiPrinter, 1);
                var refreshStatus = form.GetType().GetMethod("RequestPrinterStatusRefresh", flags);
                refreshStatus.Invoke(form, null);
                Check(statusLabel.Text.Contains("click Apply"),
                    "Unapplied default LAN address is not polled");
                uiPrinter.GetType().GetProperty("ConnType").SetValue(uiPrinter, 3);
                uiPrinter.GetType().GetProperty("PrinterName").SetValue(uiPrinter, null);
                refreshStatus.Invoke(form, null);
                Check(statusLabel.Text.Contains("select printer"),
                    "USB status asks for a printer selection");
                uiPrinter.GetType().GetProperty("PrinterName").SetValue(uiPrinter, "Test queue");
                refreshStatus.Invoke(form, null);
                Check(statusLabel.Text.Contains("queue selected"),
                    "USB status identifies a selected queue without claiming hardware readiness");
                form.GetType().GetField("_statusMonitoringStarted", flags).SetValue(form, false);
                var rotationList = (System.Windows.Forms.ComboBox)form.GetType()
                    .GetField("cmbRotation", flags).GetValue(form);
                Check(rotationList.Items.Count == 4, "Rotation list has four options");
                Type templateType = application.GetType("GodexIndustrial.LabelTemplate");
                object uiTemplate = Activator.CreateInstance(templateType);
                templateType.GetProperty("Name").SetValue(uiTemplate, "Rotation test");
                form.GetType().GetField("_currentTemplate", flags).SetValue(form, uiTemplate);
                var apply = form.GetType().GetMethod("ApplyTemplateToUI", flags);
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    templateType.GetProperty("Rotation").SetValue(uiTemplate, rotation);
                    apply.Invoke(form, new[] { uiTemplate });
                    Check(rotationList.SelectedIndex == rotation,
                        $"Saved rotation {rotation} appears in the UI");
                    Check(rotationList.Items[rotation].ToString() == LabelRotation.Labels[rotation],
                        $"Rotation {rotation} has the expected label");
                    int next = (rotation + 1) % 4;
                    rotationList.SelectedIndex = next;
                    Check((int)templateType.GetProperty("Rotation").GetValue(uiTemplate) == next,
                        $"UI selection {next} updates the template");
                    Check(printFontList.SelectedItem.ToString() == "FNT — A.FNT",
                        "Template changes do not alter the selected printer font");
                }

                form.GetType().GetMethod("InvalidatePrinterFontChoices", flags)
                    .Invoke(form, null);
                Check(printFontList.Items.Count == 1 &&
                    printFontList.SelectedItem.ToString() == "A (без перевірки)" &&
                    fontsList.Items.Count == 0,
                    "Changing printer connection clears its cached font catalog and choice");
                var setPrinting = form.GetType().GetMethod("SetPrinting", flags);
                var connectionGroup = (System.Windows.Forms.Control)form.GetType()
                    .GetField("groupBox1", flags).GetValue(form);
                setPrinting.Invoke(form, new object[] { true });
                Check(!printFontList.Enabled && !fontRefresh.Enabled &&
                    !connectionGroup.Enabled,
                    "Printer and font controls are locked during printing");
                setPrinting.Invoke(form, new object[] { false });
                var offsets = (List<int>)templateType.GetProperty("XOffsets").GetValue(uiTemplate);
                if (offsets.Count == 0) offsets.Add(250);
                else offsets[0] = 250;
                templateType.GetProperty("YOffset").SetValue(uiTemplate, 150);
                templateType.GetProperty("LabelWidth").SetValue(uiTemplate, 60);
                templateType.GetProperty("LabelLength").SetValue(uiTemplate, 40);
                var previewType = application.GetType("GodexIndustrial.LabelPreviewForm");
                var draw = previewType.GetMethod("DrawPreview", flags);
                var elevenRows = new List<string[]>();
                for (int i = 0; i < 11; i++) elevenRows.Add(new[] { "LABEL " + i });
                using (var pagedPreview = (System.Windows.Forms.Form)Activator.CreateInstance(
                    previewType, new object[] { uiTemplate, elevenRows, "" }))
                {
                    var pageLabel = (System.Windows.Forms.Label)previewType
                        .GetField("_pageLabel", flags).GetValue(pagedPreview);
                    var nextButton = (System.Windows.Forms.Button)previewType
                        .GetField("_next", flags).GetValue(pagedPreview);
                    Check(pageLabel.Text.Contains("1–10 of 11"), "First preview page shows ten labels");
                    typeof(System.Windows.Forms.Button).GetMethod("OnClick",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(nextButton, new object[] { EventArgs.Empty });
                    Check(pageLabel.Text.Contains("11–11 of 11"), "Second preview page reaches the last label");
                }
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    templateType.GetProperty("Rotation").SetValue(uiTemplate, rotation);
                    using (var preview = (System.Windows.Forms.Form)Activator.CreateInstance(
                        previewType, new object[] { uiTemplate,
                            new List<string[]> { new[] { "ROTATION" } }, "" }))
                    {
                        var viewport = (System.Windows.Forms.Panel)previewType
                            .GetField("_viewport", flags).GetValue(preview);
                        var canvas = (System.Windows.Forms.Panel)viewport.Controls[0];
                        canvas.Dock = System.Windows.Forms.DockStyle.None;
                        canvas.Size = new System.Drawing.Size(600, 300);
                        using (var bitmap = new System.Drawing.Bitmap(600, 300))
                        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                        {
                            draw.Invoke(preview, new object[] { canvas,
                                new System.Windows.Forms.PaintEventArgs(graphics, new System.Drawing.Rectangle(0, 0, 600, 300)) });
                            int minX = 600, minY = 300, maxX = -1, maxY = -1;
                            for (int py = 0; py < bitmap.Height; py++)
                            for (int px = 0; px < bitmap.Width; px++)
                            {
                                System.Drawing.Color color = bitmap.GetPixel(px, py);
                                if (color.R >= 80 || color.G >= 80 || color.B >= 80) continue;
                                minX = Math.Min(minX, px); minY = Math.Min(minY, py);
                                maxX = Math.Max(maxX, px); maxY = Math.Max(maxY, py);
                            }
                            Check(maxX >= minX && maxY >= minY, $"Preview draws text at {rotation * 90} degrees");
                            int width = maxX - minX + 1, height = maxY - minY + 1;
                            Check(rotation % 2 == 0 ? width > height * 1.3 : height > width * 1.3,
                                $"Preview orientation matches {rotation * 90} degrees");
                        }
                    }
                }
            }
            Console.WriteLine($"{_passed} checks passed");
        }
    }
}
