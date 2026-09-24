using FontAwesome.Sharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GodexIndustrial
{
    public partial class Form1 : Form
    {
        private IconButton currentBtn;
        private Panel leftBorderBtn;


        //Fields
        LabelPrinter _printer = new LabelPrinter();
        List<LabelTemplate> _templates = new List<LabelTemplate>();
        LabelTemplate _currentTemplate;
        private EventLogger _logger;
        private bool _isUpdatingUI = false;
        private SavedPrinterSettings _settings;
        private bool _lanAddressApplied;
        private bool _isPrinting;
        private readonly Timer _printerStatusTimer;
        private bool _statusMonitoringStarted;
        private bool _statusCheckInProgress;
        private bool _statusRefreshPending;
        private int _statusGeneration;
        private readonly List<string[]> _labelRows = new List<string[]>();
        private bool _renderingRows;
        private ListView _fontsList;
        private Label _fontsStatus;
        private Button _refreshFontsButton;
        private Button _uploadFontButton;
        private Button _deleteFontButton;
        private bool _fontsLoading;
        private bool _fontCatalogLoaded;
        private int _fontConnectionGeneration;


        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTCLIENT = 1;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);

                if ((int)m.Result == HTCLIENT)
                {
                    Point pos = this.PointToClient(Cursor.Position);
                    int grip = 8; // ширина зони ресайзу

                    if (pos.X <= grip && pos.Y <= grip)
                        m.Result = (IntPtr)HTTOPLEFT;
                    else if (pos.X >= Width - grip && pos.Y <= grip)
                        m.Result = (IntPtr)HTTOPRIGHT;
                    else if (pos.X <= grip && pos.Y >= Height - grip)
                        m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (pos.X >= Width - grip && pos.Y >= Height - grip)
                        m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (pos.X <= grip)
                        m.Result = (IntPtr)HTLEFT;
                    else if (pos.X >= Width - grip)
                        m.Result = (IntPtr)HTRIGHT;
                    else if (pos.Y <= grip)
                        m.Result = (IntPtr)HTTOP;
                    else if (pos.Y >= Height - grip)
                        m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }

            base.WndProc(ref m);
        }

        public Form1()
        {
            InitializeComponent();
            btnRefreshPrinterFonts.Click += async (sender, args) => await RefreshFontsAsync();
            cmbPrinterFont.SelectedIndexChanged += (sender, args) => UpdateSelectedFontHint();
            toolTip1.SetToolTip(btnRefreshPrinterFonts, "Прочитати список шрифтів з принтера.");
            InitializeFontsPage();
            _printerStatusTimer = new Timer(components) { Interval = 15000 };
            _printerStatusTimer.Tick += (sender, args) => RequestPrinterStatusRefresh();
            leftBorderBtn = new Panel();
            leftBorderBtn.Size = new Size(7, 60);
            panelMenu.Controls.Add(leftBorderBtn);
            //form enhance
            this.Text = string.Empty;
            this.ControlBox = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            this.MaximizedBounds = Screen.FromHandle(this.Handle).WorkingArea;
           
            ActivateButton(iconLabel, RGBColors.color1);

            _settings = PrinterSettingsStore.Load();
            _lanAddressApplied = _settings.LanAddressApplied;
            _logger = new EventLogger(tbLog);
            tbIP.Text = _settings.IpAddress;
            tbIP.TextChanged += (sender, args) =>
            {
                InvalidatePrinterFontChoices();
                RequestPrinterStatusRefresh();
            };
            cmbSerialPorts.SelectedIndexChanged += cmbSerialPorts_SelectedIndexChanged;
            cmbBaudRate.SelectedIndexChanged += cmbBaudRate_SelectedIndexChanged;
            cmbPrinters.SelectedIndexChanged += cmbPrinters_SelectedIndexChanged;
            FormClosing += Form1_FormClosing;
            UpdateDataGridViewColumns(1);
            LoadTemplates(_settings.TemplateName);
            PopulatePrinterList();
            HookSyncEvents();
            myDataGridView.CellValueChanged += (sender, args) =>
            {
                if (!_renderingRows && args.RowIndex >= 0 && args.RowIndex < _labelRows.Count &&
                    args.ColumnIndex >= 0 && args.ColumnIndex < 8)
                    _labelRows[args.RowIndex][args.ColumnIndex] =
                        myDataGridView.Rows[args.RowIndex].Cells[args.ColumnIndex].Value?.ToString();
            };
            myDataGridView.RowsRemoved += (sender, args) =>
            {
                if (!_renderingRows && args.RowIndex >= 0 && args.RowIndex < _labelRows.Count)
                    _labelRows.RemoveRange(args.RowIndex, Math.Min(args.RowCount, _labelRows.Count - args.RowIndex));
            };
        }

        private void InitializeFontsPage()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(18)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            header.Controls.Add(new Label
            {
                Text = "Printer fonts",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            _uploadFontButton = new Button
            {
                Text = "Завантажити",
                Size = new Size(128, 36),
                Anchor = AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(32, 89, 83),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F)
            };
            _uploadFontButton.FlatAppearance.BorderSize = 0;
            _uploadFontButton.Click += UploadFont_Click;
            toolTip1.SetToolTip(_uploadFontButton,
                "Вибрати встановлений шрифт Windows і завантажити його у принтер.");
            header.Controls.Add(_uploadFontButton, 1, 0);
            _deleteFontButton = new Button
            {
                Text = "Видалити",
                Size = new Size(108, 36),
                Anchor = AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(185, 62, 54),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                Enabled = false
            };
            _deleteFontButton.FlatAppearance.BorderSize = 0;
            _deleteFontButton.Click += DeleteFont_Click;
            toolTip1.SetToolTip(_deleteFontButton,
                "Видалити вибраний шрифт із пам’яті принтера.");
            header.Controls.Add(_deleteFontButton, 2, 0);
            _refreshFontsButton = new Button
            {
                Text = "Оновити",
                Size = new Size(120, 36),
                Anchor = AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 128, 0),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F)
            };
            _refreshFontsButton.FlatAppearance.BorderSize = 0;
            _refreshFontsButton.Click += async (sender, args) => await RefreshFontsAsync();
            header.Controls.Add(_refreshFontsButton, 3, 0);

            _fontsStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Натисніть «Оновити» для списку або «Завантажити», щоб вибрати шрифт Windows.",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 9F)
            };
            _fontsList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                HideSelection = false,
                Font = new Font("Segoe UI", 10F),
                UseCompatibleStateImageBehavior = false
            };
            _fontsList.Columns.Add("Type", 100);
            _fontsList.Columns.Add("File Name", 300);
            _fontsList.Columns.Add("File Size", 100);
            _fontsList.Groups.Add(new ListViewGroup("FNT", HorizontalAlignment.Left) { Name = "FNT" });
            _fontsList.Groups.Add(new ListViewGroup("TTF", HorizontalAlignment.Left) { Name = "TTF" });
            _fontsList.SelectedIndexChanged += (sender, args) => UpdateDeleteFontButton();

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(_fontsList, 0, 1);
            layout.Controls.Add(_fontsStatus, 0, 2);
            tabFonts.Controls.Add(layout);
        }

        private void iconFonts_Click(object sender, EventArgs e)
        {
            ActivateButton(sender, RGBColors.color1);
            tabControl1.SelectedTab = tabFonts;
        }

        private bool FontConnectionReady()
        {
            if (_printer.ConnType == 3)
            {
                _fontsStatus.Text = "Для керування шрифтами виберіть LAN або COM.";
                return false;
            }
            if (_printer.ConnType == 1 &&
                (!_lanAddressApplied || string.IsNullOrWhiteSpace(_printer.IpAddr) ||
                 !string.Equals(tbIP.Text.Trim(), _printer.IpAddr, StringComparison.Ordinal)))
            {
                _fontsStatus.Text = "Спочатку застосуйте LAN-адресу у Printer connection.";
                return false;
            }
            try
            {
                _printer.ValidateConnection();
                return true;
            }
            catch (Exception ex)
            {
                _fontsStatus.Text = ex.Message;
                return false;
            }
        }

        private void InvalidatePrinterFontChoices()
        {
            _fontConnectionGeneration++;
            _fontCatalogLoaded = false;
            _fontsList.Items.Clear();
            _fontsStatus.Text = "Підключення змінено. Натисніть «Оновити» для нового списку.";
            UpdateDeleteFontButton();
            cmbPrinterFont.Items.Clear();
            cmbPrinterFont.Items.Add("A (без перевірки)");
            cmbPrinterFont.SelectedIndex = 0;
            lblPrinterFontHint.Text = "Натисніть ↻, щоб прочитати шрифти принтера.";
        }

        private void UpdateSelectedFontHint()
        {
            var font = cmbPrinterFont.SelectedItem as PrinterFontInfo;
            if (font == null)
            {
                lblPrinterFontHint.Text = _fontCatalogLoaded
                    ? "Виберіть шрифт зі списку принтера."
                    : "Натисніть ↻, щоб прочитати шрифти принтера.";
                return;
            }

            lblPrinterFontHint.Text = font.Type == "FNT"
                ? "FNT: фіксований розмір bitmap-шрифту."
                : "TTF: розмір задається полем Font size.";
        }

        private void PopulatePrinterFontChoices(PrinterFontCatalog catalog)
        {
            var previous = cmbPrinterFont.SelectedItem as PrinterFontInfo;
            bool hadCatalog = _fontCatalogLoaded;
            cmbPrinterFont.BeginUpdate();
            try
            {
                cmbPrinterFont.Items.Clear();
                foreach (PrinterFontInfo font in catalog.Fonts)
                    if (font.TryGetPrintSlot(out _))
                        cmbPrinterFont.Items.Add(font);

                int selected = -1;
                if (hadCatalog && previous != null)
                {
                    for (int i = 0; i < cmbPrinterFont.Items.Count; i++)
                    {
                        var candidate = (PrinterFontInfo)cmbPrinterFont.Items[i];
                        if (candidate.Type == previous.Type && candidate.Name == previous.Name)
                        {
                            selected = i;
                            break;
                        }
                    }
                }
                else if (!hadCatalog)
                {
                    for (int i = 0; i < cmbPrinterFont.Items.Count; i++)
                    {
                        var candidate = (PrinterFontInfo)cmbPrinterFont.Items[i];
                        if (candidate.Type == "TTF" &&
                            candidate.TryGetPrintSlot(out char slot) && slot == 'A')
                        {
                            selected = i;
                            break;
                        }
                    }
                    if (selected < 0)
                    {
                        for (int i = 0; i < cmbPrinterFont.Items.Count; i++)
                            if (((PrinterFontInfo)cmbPrinterFont.Items[i]).Type == "TTF")
                            {
                                selected = i;
                                break;
                            }
                    }
                    if (selected < 0 && cmbPrinterFont.Items.Count > 0) selected = 0;
                }

                _fontCatalogLoaded = true;
                cmbPrinterFont.SelectedIndex = selected;
            }
            finally
            {
                cmbPrinterFont.EndUpdate();
            }
            UpdateSelectedFontHint();
        }

        private void ShowFontCatalog(PrinterFontCatalog catalog)
        {
            _fontsList.BeginUpdate();
            try
            {
                _fontsList.Items.Clear();
                foreach (PrinterFontInfo font in catalog.Fonts)
                {
                    var item = new ListViewItem(font.Type) { Tag = font };
                    item.SubItems.Add(font.Name);
                    item.SubItems.Add(string.IsNullOrEmpty(font.Size) ? "—" : font.Size);
                    item.Group = _fontsList.Groups[font.Type];
                    _fontsList.Items.Add(item);
                }
            }
            finally
            {
                _fontsList.EndUpdate();
            }

            PopulatePrinterFontChoices(catalog);
            _fontsStatus.Text = catalog.Fonts.Count + " font(s)" +
                (string.IsNullOrEmpty(catalog.FreeMemoryKb)
                    ? string.Empty : "  •  Free flash memory: " + catalog.FreeMemoryKb + " KB") +
                "  •  — means the printer did not report a file size.";
        }

        private void UpdateDeleteFontButton()
        {
            if (_deleteFontButton == null || _fontsList == null) return;
            PrinterFontInfo selected = _fontsList.SelectedItems.Count == 1
                ? _fontsList.SelectedItems[0].Tag as PrinterFontInfo : null;
            _deleteFontButton.Enabled = !_fontsLoading && !_isPrinting &&
                selected != null && selected.TryBuildDeleteCommand(out _);
        }

        private async Task RefreshFontsAsync()
        {
            if (_fontsLoading || _isPrinting) return;
            if (!FontConnectionReady())
            {
                lblPrinterFontHint.Text = _fontsStatus.Text;
                return;
            }

            int generation = _fontConnectionGeneration;
            _fontsLoading = true;
            btnRefreshPrinterFonts.Enabled = false;
            _refreshFontsButton.Enabled = false;
            _uploadFontButton.Enabled = false;
            _deleteFontButton.Enabled = false;
            _fontsStatus.Text = "Reading font list from printer...";
            lblPrinterFontHint.Text = "Читаємо шрифти принтера...";
            try
            {
                PrinterFontCatalog catalog = await _printer.QueryPrinterFontsAsync();
                if (IsDisposed || Disposing || generation != _fontConnectionGeneration) return;
                ShowFontCatalog(catalog);
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing || generation != _fontConnectionGeneration) return;
                _fontsStatus.Text = "Cannot read printer fonts: " + ex.Message;
                lblPrinterFontHint.Text = "Не вдалося прочитати шрифти принтера.";
                _logger.Log("Font list: " + ex.Message);
            }
            finally
            {
                _fontsLoading = false;
                if (!IsDisposed && !Disposing)
                {
                    _refreshFontsButton.Enabled = true;
                    btnRefreshPrinterFonts.Enabled = true;
                    _uploadFontButton.Enabled = true;
                    UpdateDeleteFontButton();
                }
            }
        }

        private async void DeleteFont_Click(object sender, EventArgs e)
        {
            if (_fontsLoading || _isPrinting || _fontsList.SelectedItems.Count != 1)
                return;
            PrinterFontInfo selected = _fontsList.SelectedItems[0].Tag as PrinterFontInfo;
            if (selected == null || !selected.TryBuildDeleteCommand(out string command) ||
                !FontConnectionReady())
                return;

            if (MessageBox.Show(this,
                "Видалити шрифт «" + selected.Name + "» з пам’яті принтера?",
                "Підтвердження видалення",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            bool sent = false;
            _fontsLoading = true;
            _refreshFontsButton.Enabled = false;
            _uploadFontButton.Enabled = false;
            _deleteFontButton.Enabled = false;
            SetPrinting(true);
            try
            {
                _fontsStatus.Text = "Перевіряю вибраний шрифт у пам’яті принтера...";
                PrinterFontCatalog before = await _printer.QueryPrinterFontsAsync();
                if (IsDisposed || Disposing) return;
                if (!before.Fonts.Any(font => font.Type == selected.Type &&
                    string.Equals(font.Name, selected.Name, StringComparison.Ordinal)))
                {
                    ShowFontCatalog(before);
                    _fontsStatus.Text = "Вибраного шрифту вже немає. Список оновлено.";
                    return;
                }

                _fontsStatus.Text = "Видаляю " + selected.Name + "...";
                await _printer.DeleteFontAsync(selected);
                sent = true;
                _logger.Log("Font delete command sent: " + command + ".");

                PrinterFontCatalog after = null;
                bool removed = false;
                for (int attempt = 0; attempt < 3 && !removed; attempt++)
                {
                    await Task.Delay(700);
                    after = await _printer.QueryPrinterFontsAsync();
                    removed = !after.Fonts.Any(font => font.Type == selected.Type &&
                        string.Equals(font.Name, selected.Name, StringComparison.Ordinal));
                }
                if (IsDisposed || Disposing) return;
                ShowFontCatalog(after);
                _fontsStatus.Text = removed
                    ? "Видалено: " + selected.Name + "."
                    : "Команду видалення надіслано, але шрифт ще є в каталозі. Натисніть «Оновити».";
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;
                _fontsStatus.Text = sent
                    ? "Команду видалення надіслано, але не вдалося перевірити результат: " + ex.Message
                    : "Не вдалося видалити шрифт: " + ex.Message;
                _logger.Log(_fontsStatus.Text);
            }
            finally
            {
                _fontsLoading = false;
                if (!IsDisposed && !Disposing)
                {
                    _refreshFontsButton.Enabled = true;
                    btnRefreshPrinterFonts.Enabled = true;
                    _uploadFontButton.Enabled = true;
                    SetPrinting(false);
                    UpdateDeleteFontButton();
                }
            }
        }

        private async void UploadFont_Click(object sender, EventArgs e)
        {
            if (_fontsLoading || _isPrinting || !FontConnectionReady()) return;

            byte[] fontData;
            string selectedName;
            try
            {
                using (var dialog = new FontDialog
                {
                    FontMustExist = true,
                    AllowSimulations = false,
                    ShowEffects = false,
                    ShowColor = false
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    using (var selectedFont = (Font)dialog.Font.Clone())
                    {
                        selectedName = selectedFont.FontFamily.Name +
                            (selectedFont.Bold ? "Bold" : string.Empty) +
                            (selectedFont.Italic ? "Italic" : string.Empty);
                        fontData = InstalledFontData.ReadTrueType(selectedFont);
                    }
                }
            }
            catch (Exception ex)
            {
                _fontsStatus.Text = "Не вдалося прочитати шрифт Windows: " + ex.Message;
                ShowError("Шрифт не підтримується", ex);
                return;
            }

            bool sent = false;
            string uploadedName = null;
            _fontsLoading = true;
            _refreshFontsButton.Enabled = false;
            _uploadFontButton.Enabled = false;
            _deleteFontButton.Enabled = false;
            SetPrinting(true);
            try
            {
                _fontsStatus.Text = "Перевіряю вільні слоти шрифтів...";
                PrinterFontCatalog before = await _printer.QueryPrinterFontsAsync();
                if (IsDisposed || Disposing) return;
                char slot = before.FindAvailableSlot("TTF");

                _fontsStatus.Text = "Завантажую " + selectedName + " у слот " + slot + "...";
                var progress = new Progress<int>(percent =>
                {
                    if (!sent && !IsDisposed && !Disposing)
                        _fontsStatus.Text = "Завантажую " + selectedName +
                            " у слот " + slot + ": " + percent + "%";
                });
                uploadedName = await _printer.UploadFontAsync(fontData, selectedName, slot, progress);
                sent = true;
                _logger.Log("Font data sent: " + uploadedName + ".");

                PrinterFontCatalog after = null;
                bool found = false;
                for (int attempt = 0; attempt < 3 && !found; attempt++)
                {
                    await Task.Delay(900);
                    after = await _printer.QueryPrinterFontsAsync();
                    found = after.Fonts.Any(font => font.Type == "TTF" &&
                        font.Name.Length > 0 &&
                        char.ToUpperInvariant(font.Name[0]) == slot);
                }
                if (IsDisposed || Disposing) return;
                ShowFontCatalog(after);
                _fontsStatus.Text = found
                    ? "Завантажено: " + uploadedName + "."
                    : "Дані передано, але шрифт ще не з’явився у каталозі. Натисніть «Оновити».";
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;
                _fontsStatus.Text = sent
                    ? "Дані передано, але не вдалося перевірити каталог: " + ex.Message
                    : "Не вдалося завантажити шрифт: " + ex.Message;
                _logger.Log(_fontsStatus.Text);
            }
            finally
            {
                _fontsLoading = false;
                if (!IsDisposed && !Disposing)
                {
                    _refreshFontsButton.Enabled = true;
                    btnRefreshPrinterFonts.Enabled = true;
                    _uploadFontButton.Enabled = true;
                    UpdateDeleteFontButton();
                    SetPrinting(false);
                }
            }
        }

        private void HookSyncEvents()
        {
            tbFontSize.TextChanged += (s, e) => SyncTemplateWithUI();
            tbYOffset.TextChanged += (s, e) => SyncTemplateWithUI();
            numColumns.ValueChanged += (s, e) => SyncTemplateWithUI();
            numLabelWidth.ValueChanged += (s, e) => SyncTemplateWithUI();
            numLabelLength.ValueChanged += (s, e) => SyncTemplateWithUI();
            numLabelGap.ValueChanged += (s, e) => SyncTemplateWithUI();
            numDarkness.ValueChanged += (s, e) => SyncTemplateWithUI();
            cmbRotation.SelectedIndexChanged += (s, e) => SyncTemplateWithUI();
            cmbPrintSpeedTemplate.SelectedIndexChanged += (s, e) => SyncTemplateWithUI();
        }

        private struct RGBColors
        {
            public static Color color1 = Color.FromArgb(255, 128, 0);
            public static Color color2 = Color.FromArgb(119, 157, 202);
        }
        private void ActivateButton(object senderBtn, Color color)
        {
            if (senderBtn != null) {
                DisableButton();
                currentBtn= (IconButton)senderBtn;
                currentBtn.BackColor = Color.FromArgb(37, 36, 81);
                currentBtn.ForeColor = color;
                currentBtn.TextAlign = ContentAlignment.MiddleCenter;
                currentBtn.IconColor = color;
                currentBtn.TextImageRelation = TextImageRelation.TextBeforeImage;
                currentBtn.ImageAlign = ContentAlignment.MiddleRight;
                //
                leftBorderBtn.BackColor = color;
                leftBorderBtn.Location = new Point(0, currentBtn.Location.Y);
                leftBorderBtn.Visible = true;
                leftBorderBtn.BringToFront();
                //
                iconCurrentChildFormIcon.IconChar = currentBtn.IconChar;
                iconCurrentChildFormIcon.IconColor = color;
                lblTitle.Text = currentBtn.Text;
            }
        }
        private void DisableButton()
        {
            if(currentBtn != null)
            {
                currentBtn.BackColor = Color.FromArgb(31, 30, 68);
                currentBtn.ForeColor = Color.Gainsboro;
                currentBtn.TextAlign = ContentAlignment.MiddleLeft;
                currentBtn.IconColor = Color.Gainsboro;
                currentBtn.TextImageRelation = TextImageRelation.ImageBeforeText;
                currentBtn.ImageAlign = ContentAlignment.MiddleLeft;
            }
        }
        [DllImport("user32.DLL", EntryPoint = "ReleaseCapture")]
        private extern static void ReleaseCapture();

        [DllImport("user32.DLL", EntryPoint = "SendMessage")]
        private extern static void SendMassage(System.IntPtr hWnd,int wMsg,int wParam, int lParam);
        
        private void LoadTemplates(string selectTemplateName = null)
        {
            _templates = TemplateManager.GetAllTemplates(message => _logger.Log(message));
            cmbTemplate.DataSource = null;
            cmbTemplate.DataSource = _templates;
            cmbTemplate.DisplayMember = "Name";

            if (_templates.Count > 0)
            {
                int indexToSelect = 0;
                if (!string.IsNullOrEmpty(selectTemplateName))
                {
                    int foundIndex = _templates.FindIndex(t => t.Name == selectTemplateName);
                    if (foundIndex != -1) indexToSelect = foundIndex;
                }

                cmbTemplate.SelectedIndex = indexToSelect;
                _currentTemplate = _templates[indexToSelect];
                ApplyTemplateToUI(_currentTemplate);
                _logger.Log($"Template loaded: {_currentTemplate.Name}");
            }
            else
            {
                _currentTemplate = null;
            }
        }

        private void pasteEXCEL(object sender, EventArgs e)
        {
            try
            {
                if (_currentTemplate == null) throw new InvalidOperationException("Select a template first.");
            myDataGridView.EndEdit();
                string text = Clipboard.ContainsText() ? Clipboard.GetText() : null;
                List<string[]> rows = LabelDataParser.ParseTsv(text, _currentTemplate.ColumnCount);
                DialogResult action = myDataGridView.Rows.Count == 0
                    ? DialogResult.No
                    : MessageBox.Show($"Import {rows.Count} rows? Yes = replace, No = append, Cancel = stop.",
                        "Import from Excel", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (action == DialogResult.Cancel) return;
                if (action == DialogResult.Yes) _labelRows.Clear();
                foreach (string[] row in rows) AddLabelRow(row, false);
                RenderRows();
                _logger.Log($"Imported {rows.Count} rows.");
            }
            catch (Exception ex)
            {
                ShowError("Cannot import clipboard data", ex);
            }
        }

        private List<string[]> GetLabelRows()
        {
            var rows = new List<string[]>();
            if (_currentTemplate == null) return rows;
            foreach (string[] row in _labelRows)
            {
                string[] values = new string[_currentTemplate.ColumnCount];
                Array.Copy(row, values, values.Length);
                if (values.Any(v => !string.IsNullOrWhiteSpace(v))) rows.Add(values);
            }
            return rows;
        }

        private void AddLabelRow(string[] cells, bool render = true)
        {
            string[] values = new string[8];
            Array.Copy(cells, values, Math.Min(cells.Length, values.Length));
            _labelRows.Add(values);
            if (render) RenderRows();
        }

        private void RenderRows()
        {
            _renderingRows = true;
            try
            {
                myDataGridView.Rows.Clear();
                foreach (string[] row in _labelRows) myDataGridView.Rows.Add(row);
            }
            finally
            {
                _renderingRows = false;
            }
        }

        private string GenerateZplScript()
        {
            if (_currentTemplate == null) throw new InvalidOperationException("Select a template first.");
            myDataGridView.EndEdit();
            SyncTemplateWithUI();
            if (_fontCatalogLoaded && !(cmbPrinterFont.SelectedItem is PrinterFontInfo))
                throw new InvalidOperationException("Виберіть завантажений шрифт принтера або оновіть список.");
            return LabelCommandBuilder.Build(_currentTemplate, GetLabelRows(),
                cmbPrinterFont.SelectedItem as PrinterFontInfo);
        }

        private async void PrintLabels(object sender, EventArgs e)
        {
            if (_isPrinting) return;
            try
            {
                string script = GenerateZplScript();
                int labelCount = GetLabelRows().Count;
                string templateName = _currentTemplate.Name;
                _printer.ValidateConnection();
                SetPrinting(true);
                await _printer.PrintAsync(script);
                _logger.Log($"Print job sent: {templateName}, {labelCount} labels.");
                MessageBox.Show("Print job sent to the printer.", "Printing", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError("Printing failed", ex);
            }
            finally
            {
                SetPrinting(false);
            }
        }

        private void SetPrinting(bool printing)
        {
            _isPrinting = printing;
            UpdateDeleteFontButton();
            iconPrint.Enabled = !printing;
            iconCalibtate.Enabled = !printing;
            myDataGridView.Enabled = !printing;
            cmbTemplate.Enabled = !printing;
            cmbPrinterFont.Enabled = !printing;
            btnRefreshPrinterFonts.Enabled = !printing && !_fontsLoading;
            _refreshFontsButton.Enabled = !printing && !_fontsLoading;
            _uploadFontButton.Enabled = !printing && !_fontsLoading;
            groupBox1.Enabled = !printing;
            groupBox2.Enabled = !printing;
            groupBox3.Enabled = !printing;
            groupBox5.Enabled = !printing;
            Cursor = printing ? Cursors.WaitCursor : Cursors.Default;
            if (printing)
            {
                _statusGeneration++;
                SetPrinterStatus("Printer: busy", Color.FromArgb(255, 219, 130),
                    "Printing or calibration is in progress.");
            }
            else RequestPrinterStatusRefresh();
        }

        private void btnGenZPL_click(object sender, EventArgs e)
        {
            try
            {
                string script = GenerateZplScript();
                List<string[]> rows = GetLabelRows();
                using (var preview = new LabelPreviewForm(_currentTemplate, rows, script))
                    preview.ShowDialog(this);
            }
            catch (Exception ex)
            {
                ShowError("Cannot preview labels", ex);
            }
        }

        private async void btnCalibrate_Click(object sender, EventArgs e)
        {
            if (_isPrinting) return;
            try
            {
                _printer.ValidateConnection();
                SetPrinting(true);
                await _printer.CalibrateAsync();
                _logger.Log("Calibration command sent.");
            }
            catch (Exception ex)
            {
                ShowError("Calibration failed", ex);
            }
            finally
            {
                SetPrinting(false);
            }
        }

        private void btnSetIP_Click(object sender, EventArgs e)
        {
            try
            {
                _printer.IpAddr = tbIP.Text.Trim();
                _lanAddressApplied = true;
                InvalidatePrinterFontChoices();
                _logger.Log($"LAN address set to {_printer.IpAddr}.");
            }
            catch (Exception ex)
            {
                ShowError("Invalid LAN address", ex);
            }
            finally
            {
                RequestPrinterStatusRefresh();
            }
        }

        private void lblPrinterStatus_Click(object sender, EventArgs e)
        {
            RequestPrinterStatusRefresh();
        }

        private void SetPrinterStatus(string message, Color color, string details)
        {
            if (IsDisposed || Disposing) return;
            lblPrinterStatus.Text = "● " + message;
            lblPrinterStatus.ForeColor = color;
            toolTip1.SetToolTip(lblPrinterStatus, details + Environment.NewLine + "Click to refresh.");
        }

        private void RequestPrinterStatusRefresh()
        {
            if (!_statusMonitoringStarted || IsDisposed || Disposing) return;
            int generation = ++_statusGeneration;
            if (_isPrinting)
            {
                SetPrinterStatus("Printer: busy", Color.FromArgb(255, 219, 130),
                    "Printing or calibration is in progress.");
                return;
            }

            int connectionType = _printer.ConnType;
            if (connectionType == 3)
            {
                if (string.IsNullOrWhiteSpace(_printer.PrinterName))
                    SetPrinterStatus("USB: select printer", Color.FromArgb(255, 219, 130),
                        "Choose an installed Windows printer.");
                else
                    SetPrinterStatus("USB: queue selected", Color.FromArgb(255, 219, 130),
                        "Windows queue: " + _printer.PrinterName +
                        ". Physical printer status is unavailable through RAW printing.");
                return;
            }
            if (connectionType == 1 &&
                (!_lanAddressApplied || string.IsNullOrWhiteSpace(_printer.IpAddr) ||
                 !string.Equals(tbIP.Text.Trim(), _printer.IpAddr, StringComparison.Ordinal)))
            {
                SetPrinterStatus("LAN: click Apply", Color.FromArgb(255, 219, 130),
                    "Apply the IPv4 address before checking printer status.");
                return;
            }
            if (connectionType == 2 && string.IsNullOrWhiteSpace(_printer.ComPortName))
            {
                SetPrinterStatus("COM: select port", Color.FromArgb(255, 219, 130),
                    "Choose a COM port before checking printer status.");
                return;
            }
            if (connectionType != 1 && connectionType != 2)
            {
                SetPrinterStatus("Printer: no connection", Color.FromArgb(255, 219, 130),
                    "Choose a printer connection type.");
                return;
            }
            if (_statusCheckInProgress)
            {
                _statusRefreshPending = true;
                SetPrinterStatus("Printer: checking...", Color.Gainsboro,
                    "Waiting for the previous status query to finish.");
                return;
            }

            _statusCheckInProgress = true;
            SetPrinterStatus("Printer: checking...", Color.Gainsboro,
                "Requesting the current status from the printer.");
            _ = CheckPrinterStatusAsync(generation, connectionType);
        }

        private async Task CheckPrinterStatusAsync(int generation, int connectionType)
        {
            string connection = connectionType == 1 ? "LAN" : "COM";
            try
            {
                string response = await _printer.QueryStatusAsync();
                if (!_statusMonitoringStarted || IsDisposed || Disposing ||
                    generation != _statusGeneration) return;

                PrinterStatusInfo status = PrinterStatusInfo.Parse(response);
                Color color = status.Kind == PrinterStatusKind.Ready
                    ? Color.FromArgb(144, 238, 175)
                    : status.Kind == PrinterStatusKind.Error
                        ? Color.FromArgb(255, 155, 155)
                        : Color.FromArgb(255, 219, 130);
                string reply = new string((response ?? string.Empty)
                    .Where(character => !char.IsControl(character)).Take(80).ToArray());
                SetPrinterStatus(connection + ": " + status.Description, color,
                    "GoDEX status code: " + (status.Code ?? "unknown") +
                    (reply.Length == 0 ? string.Empty : Environment.NewLine + "Reply: " + reply));
            }
            catch (Exception ex)
            {
                if (_statusMonitoringStarted && !IsDisposed && !Disposing &&
                    generation == _statusGeneration)
                    SetPrinterStatus(connection + ": no response", Color.FromArgb(255, 155, 155),
                        ex.Message);
            }
            finally
            {
                _statusCheckInProgress = false;
                if (_statusRefreshPending && _statusMonitoringStarted && !IsDisposed && !Disposing)
                {
                    _statusRefreshPending = false;
                    RequestPrinterStatusRefresh();
                }
            }
        }

        private void ShowError(string action, Exception ex)
        {
            _logger.Log($"{action}: {ex.Message}");
            MessageBox.Show(ex.Message, action, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            cmbSerialPorts.DataSource = _printer.EnumComPorts;
            cmbBaudRate.DataSource = _printer.BaudRateValues;
            if (!string.IsNullOrWhiteSpace(_settings.ComPort) && cmbSerialPorts.Items.Contains(_settings.ComPort))
                cmbSerialPorts.SelectedItem = _settings.ComPort;
            if (cmbBaudRate.Items.Contains(_settings.BaudRate.ToString())) cmbBaudRate.SelectedItem = _settings.BaudRate.ToString();
            try { _printer.IpAddr = tbIP.Text.Trim(); }
            catch (Exception ex)
            {
                tbIP.Text = "172.16.1.13";
                _printer.IpAddr = tbIP.Text;
                _lanAddressApplied = false;
                _logger.Log($"Saved LAN address ignored: {ex.Message}");
            }
            if (_settings.ConnectionType == 2) radioButton2.Checked = true;
            else if (_settings.ConnectionType == 3) radioButton3.Checked = true;
            else radioButton1.Checked = true;
            _statusMonitoringStarted = true;
            _printerStatusTimer.Start();
            RequestPrinterStatusRefresh();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isPrinting)
            {
                e.Cancel = true;
                MessageBox.Show("Wait for the current printer operation to finish.", "Printer busy",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _statusMonitoringStarted = false;
            _statusGeneration++;
            _printerStatusTimer.Stop();
            try
            {
                _settings.ConnectionType = _printer.ConnType;
                _settings.IpAddress = _printer.IpAddr;
                _settings.LanAddressApplied = _lanAddressApplied;
                _settings.ComPort = _printer.ComPortName;
                _settings.BaudRate = _printer.BaudRate;
                _settings.PrinterName = _printer.PrinterName;
                _settings.TemplateName = _currentTemplate?.Name;
                PrinterSettingsStore.Save(_settings);
            }
            catch (Exception ex)
            {
                ShowError("Cannot save settings", ex);
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                _printer.ConnType = 1;
                InvalidatePrinterFontChoices();
                RequestPrinterStatusRefresh();
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked)
            {
                _printer.ConnType = 2;
                InvalidatePrinterFontChoices();
                RequestPrinterStatusRefresh();
            }
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton3.Checked)
            {
                _printer.ConnType = 3;
                InvalidatePrinterFontChoices();
                RequestPrinterStatusRefresh();
            }
        }

        private void cmbSerialPorts_SelectedIndexChanged(object sender, EventArgs e)
        {
            _printer.ComPortName = cmbSerialPorts.SelectedItem as string;
            InvalidatePrinterFontChoices();
            RequestPrinterStatusRefresh();
        }

        private void cmbBaudRate_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (int.TryParse(cmbBaudRate.SelectedItem as string, out int baud)) _printer.BaudRate = baud;
            InvalidatePrinterFontChoices();
            RequestPrinterStatusRefresh();
        }

        private void PopulatePrinterList()
        {
            cmbPrinters.Items.Clear();
            foreach (string printer in PrinterSettings.InstalledPrinters)
                cmbPrinters.Items.Add(printer);
            if (cmbPrinters.Items.Count == 0) return;
            string selected = PrinterSelection.Choose(cmbPrinters.Items,
                _settings.PrinterName, new PrinterSettings().PrinterName);
            if (selected != null) cmbPrinters.SelectedItem = selected;
        }

        private void cmbPrinters_SelectedIndexChanged(object sender, EventArgs e)
        {
            _printer.PrinterName = cmbPrinters.SelectedItem as string;
            InvalidatePrinterFontChoices();
            RequestPrinterStatusRefresh();
        }

        private void cmbTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            _currentTemplate = cmbTemplate.SelectedItem as LabelTemplate;
            if (_currentTemplate != null)
            {
                ApplyTemplateToUI(_currentTemplate);
            }
        }

        private void ApplyTemplateToUI(LabelTemplate t)
        {
            _isUpdatingUI = true;
            try
            {
                tbFontSize.Text = t.FontSize.ToString();
                tbYOffset.Text = t.YOffset.ToString();
                numColumns.Value = t.ColumnCount;

                // Detailed params
                numLabelWidth.Value = t.LabelWidth;
                numLabelLength.Value = t.LabelLength;
                numLabelGap.Value = t.LabelGap;
                numDarkness.Value = t.Darkness;
                cmbRotation.SelectedIndex = t.Rotation;
                if (t.PrintSpeed >= 0 && t.PrintSpeed < cmbPrintSpeedTemplate.Items.Count)
                    cmbPrintSpeedTemplate.SelectedIndex = t.PrintSpeed;
                else
                    cmbPrintSpeedTemplate.SelectedIndex = 2; // Default 101.6 mm/s

                PopulateXOffsetPanel(t.ColumnCount, t.XOffsets);
                UpdateDataGridViewColumns(t.ColumnCount);
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }

        private void PopulateXOffsetPanel(int count, List<int> xOffsets)
        {
            panelXOffsets.Controls.Clear();

            int spacingX = 5; // горизонтальна відстань між картками
            int spacingY = 2;  // відстань між Label і TextBox
            int currentLeft = 5; // початкова позиція по горизонталі
            int top = 5;         // позиція зверху

            for (int i = 0; i < count; i++)
            {
                // Label над TextBox
                Label lbl = new Label()
                {
                    Text = $"X{i + 1}",
                    AutoSize = true,
                    Left = currentLeft,
                    Top = top,
                    Font = new Font("Segoe UI", 12)
                };

                NumericUpDown tb = new NumericUpDown()
                {
                    Name = $"tbXOffset{i}",
                    Width = 50,
                    Left = currentLeft,
                    Top = lbl.Bottom + spacingY,
                    Font = new Font("Segoe UI", 12),
                    Maximum = 500
                };

                tb.Value = (i < xOffsets.Count) ? xOffsets[i] : 0;
                tb.ValueChanged += (s, e) => SyncTemplateWithUI();

                panelXOffsets.Controls.Add(lbl);
                panelXOffsets.Controls.Add(tb);

                // Зрушуємо currentLeft для наступної картки
                currentLeft += tb.Width + spacingX;
            }
        }

        private void UpdateDataGridViewColumns(int count)
        {
            // Keep all eight columns in the grid so switching templates never drops entered values.
            while (myDataGridView.Columns.Count < 8)
            {
                int number = myDataGridView.Columns.Count + 1;
                myDataGridView.Columns.Add($"Col{number}", $"Col {number}");
            }
            for (int i = 0; i < myDataGridView.Columns.Count; i++)
                myDataGridView.Columns[i].Visible = i < count;
        }

        private void btnSaveTemplate_Click(object sender, EventArgs e)
        {
            if (_currentTemplate == null)
            {
                btnNewTemplate_Click(sender, e);
                return;
            }
            try
            {
                SyncTemplateWithUI();
                TemplateManager.SaveTemplate(_currentTemplate);
                _logger.Log($"Template saved: {_currentTemplate.Name}");
            }
            catch (Exception ex)
            {
                ShowError("Cannot save template", ex);
            }
        }

        private void SyncTemplateWithUI()
        {
            if (_currentTemplate == null || _isUpdatingUI) return;

            if (int.TryParse(tbFontSize.Text, out int fontSize)) _currentTemplate.FontSize = fontSize;
            if (int.TryParse(tbYOffset.Text, out int yOffset)) _currentTemplate.YOffset = yOffset;
            
            _currentTemplate.ColumnCount = (int)numColumns.Value;
            _currentTemplate.LabelWidth = (int)numLabelWidth.Value;
            _currentTemplate.LabelLength = (int)numLabelLength.Value;
            _currentTemplate.LabelGap = (int)numLabelGap.Value;
            _currentTemplate.Darkness = (int)numDarkness.Value;
            _currentTemplate.Rotation = cmbRotation.SelectedIndex;
            _currentTemplate.PrintSpeed = cmbPrintSpeedTemplate.SelectedIndex;

            _currentTemplate.XOffsets.Clear();
            for (int i = 0; i < _currentTemplate.ColumnCount; i++)
            {
                Control[] ctrls = panelXOffsets.Controls.Find($"tbXOffset{i}", true);
                if (ctrls.Length > 0 && ctrls[0] is NumericUpDown tb)
                {
                    _currentTemplate.XOffsets.Add((int)tb.Value);
                }
                else
                {
                    _currentTemplate.XOffsets.Add(0);
                }
            }
        }

        private void btnNewTemplate_Click(object sender, EventArgs e)
        {
            string name = Microsoft.VisualBasic.Interaction.InputBox("Enter new template name:", "New Template", "Template " + (_templates.Count + 1));
            if (string.IsNullOrWhiteSpace(name)) return;
            if (_templates.Exists(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Template with this name already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                var newTemplate = new LabelTemplate { Name = name };
                newTemplate.XOffsets.Add(0);
                TemplateManager.SaveTemplate(newTemplate);
                LoadTemplates(name);
                _logger.Log($"New template created: {name}");
            }
            catch (Exception ex)
            {
                ShowError("Cannot create template", ex);
            }
        }

        private void btnRenameTemplate_Click(object sender, EventArgs e)
        {
            if (_currentTemplate == null) return;
            string newName = Microsoft.VisualBasic.Interaction.InputBox("Enter new name for template:", "Rename Template", _currentTemplate.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == _currentTemplate.Name) return;
            if (_templates.Exists(t => t.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Template with this name already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                string oldName = _currentTemplate.Name;
                TemplateManager.RenameTemplate(_currentTemplate, newName);
                LoadTemplates(newName);
                _logger.Log($"Template renamed: {oldName} to {newName}");
            }
            catch (Exception ex)
            {
                ShowError("Cannot rename template", ex);
            }
        }

        private void btnDeleteTemplate_Click(object sender, EventArgs e)
        {
            if (_currentTemplate == null) return;
            if (MessageBox.Show($"Delete template '{_currentTemplate.Name}'?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                string name = _currentTemplate.Name;
                TemplateManager.DeleteTemplate(name);
                LoadTemplates();
                _logger.Log($"Template deleted: {name}");
            }
            catch (Exception ex)
            {
                ShowError("Cannot delete template", ex);
            }
        }

        private void numColumns_ValueChanged(object sender, EventArgs e)
        {
            if (_isUpdatingUI) return;

            if (_currentTemplate != null)
            {
                int newCount = (int)numColumns.Value;
                UpdateXOffsetFieldsManual(newCount);
                UpdateDataGridViewColumns(newCount);
            }
        }

        private void UpdateXOffsetFieldsManual(int count)
        {
            List<int> currentVals = new List<int>();
            for (int i = 0; i < 20; i++) // Check more possible fields
            {
                Control[] ctrls = panelXOffsets.Controls.Find($"tbXOffset{i}", true);
                if (ctrls.Length > 0 && ctrls[0] is NumericUpDown tb)
                {
                    currentVals.Add((int)tb.Value);
                }
            }

            PopulateXOffsetPanel(count, currentVals);
        }



        private void iconButton1_Click(object sender, EventArgs e)
        {
            ActivateButton(sender, RGBColors.color1);
            tabControl1.SelectedTab = tabPage1;
        }

        private void iconButton2_Click(object sender, EventArgs e)
        {
            ActivateButton(sender, RGBColors.color1);
            tabControl1.SelectedTab = tabPage2;
        }

        private void iconButton3_Click(object sender, EventArgs e)
        {
            ActivateButton(sender, RGBColors.color1);
            tabControl1.SelectedTab = tabPage3;
        }

        private void iconButton4_Click(object sender, EventArgs e)
        {
            ActivateButton(sender, RGBColors.color1);
            tabControl1.SelectedTab = tabPage4;
        }

        private void iconButton5_Click(object sender, EventArgs e)
        {
            ActivateButton(sender, RGBColors.color2);
            tabControl1.SelectedTab = tabPage5;
        }

        private void panelTitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            ReleaseCapture();
            SendMassage(this.Handle, 0x112, 0xf012, 0);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void iconButton7_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void iconButton6_Click(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
                WindowState = FormWindowState.Maximized;
            else
                WindowState = FormWindowState.Normal;
        }

        private void iconAddSeries_Click(object sender, EventArgs e)
        {
            if (_currentTemplate == null)
            {
                MessageBox.Show("Please select a template first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (frmAddSeries frm = new frmAddSeries())
            {
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    List<string> series = frm.GenerateSeries();
                    int columnCount = _currentTemplate.ColumnCount;

                    // Group items by columns
                    for (int i = 0; i < series.Count; i += columnCount)
                    {
                        string[] rowValues = new string[columnCount];
                        for (int j = 0; j < columnCount; j++)
                        {
                            if (i + j < series.Count)
                            {
                                rowValues[j] = series[i + j];
                            }
                        }
                        AddLabelRow(rowValues, false);
                    }

                    RenderRows();
                    _logger.Log($"Generated series: {frm.Prefix}{frm.StartValue} to {frm.Prefix}{frm.EndValue} (Step: {frm.StepValue})");
                }
            }
        }
    }
}




