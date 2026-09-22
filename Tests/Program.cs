using System;
using System.Collections.Generic;
using System.IO;
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

        [STAThread]
        private static void Main()
        {
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
            using (var form = (IDisposable)Activator.CreateInstance(application.GetType("GodexIndustrial.Form1")))
            {
                Check(form != null, "Main form opens without a saved printer");
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
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
                }

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
