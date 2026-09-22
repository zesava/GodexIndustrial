using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace GodexIndustrial
{
    internal sealed class LabelPreviewForm : Form
    {
        private const int PrinterDpi = 203;
        private const int PageSize = 10;
        private static readonly Color BackgroundColor = Color.FromArgb(34, 33, 74);
        private static readonly Color HeaderColor = Color.FromArgb(26, 25, 62);
        private static readonly Color InputColor = Color.FromArgb(37, 36, 81);
        private static readonly Color SecondaryButtonColor = Color.FromArgb(31, 30, 68);
        private static readonly Color AccentColor = Color.FromArgb(255, 128, 0);
        private readonly LabelTemplate _template;
        private readonly IReadOnlyList<string[]> _rows;
        private readonly Panel _viewport;
        private readonly Panel _canvas;
        private readonly Label _pageLabel;
        private readonly Button _previous;
        private readonly Button _next;
        private int _page;
        private float _pixelsPerMillimeter;
        private float _labelWidthPixels;
        private float _labelLengthPixels;
        private float _gapPixels;
        private float _slotHeight;

        public LabelPreviewForm(LabelTemplate template, IReadOnlyList<string[]> rows, string commands)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (rows == null || rows.Count == 0) throw new ArgumentException("There are no labels to preview.", nameof(rows));
            _template = template;
            _rows = rows;
            Text = $"Label preview — {rows.Count} labels";
            StartPosition = FormStartPosition.CenterParent;
            Width = 900;
            Height = 660;
            MinimumSize = new Size(600, 450);
            BackColor = BackgroundColor;
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 11F);
            FormBorderStyle = FormBorderStyle.None;

            var titleBar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = HeaderColor };
            var title = new Label
            {
                AutoSize = true, Location = new Point(12, 9), Text = "Label preview",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = Color.Gainsboro
            };
            var close = new Button { Dock = DockStyle.Right, Width = 45, Text = "×", DialogResult = DialogResult.Cancel };
            StyleButton(close, false);
            close.BackColor = HeaderColor;
            close.FlatAppearance.BorderSize = 0;
            close.Click += (sender, args) => Close();
            titleBar.Controls.Add(title);
            titleBar.Controls.Add(close);
            CancelButton = close;

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill, BackColor = BackgroundColor, ForeColor = Color.Gainsboro,
                DrawMode = TabDrawMode.OwnerDrawFixed, SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(180, 36), Padding = new Point(12, 4)
            };
            tabs.DrawItem += DrawTab;
            var previewPage = new TabPage("Labels") { BackColor = BackgroundColor, Padding = new Padding(8) };
            var commandsPage = new TabPage("Printer commands") { BackColor = BackgroundColor, Padding = new Padding(8) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var header = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor };
            var note = new Label
            {
                Dock = DockStyle.Top, Height = 45, ForeColor = Color.Gainsboro,
                Text = $"{template.LabelWidth} × {template.LabelLength} mm label · {template.LabelGap} mm gap · 203 dpi. " +
                    "Solid frame: label; gray: gap; dashed: text bounds; red: outside label.",
                Padding = new Padding(8, 4, 0, 0)
            };
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 42, WrapContents = false, BackColor = BackgroundColor
            };
            _previous = new Button { Text = "Previous", Size = new Size(110, 35) };
            _next = new Button { Text = "Next", Size = new Size(110, 35) };
            StyleButton(_previous, false);
            StyleButton(_next, true);
            _pageLabel = new Label
            {
                AutoSize = true, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 8, 8, 0), ForeColor = Color.Gainsboro
            };
            _previous.Click += (sender, args) => { if (_page > 0) { _page--; UpdatePage(); } };
            _next.Click += (sender, args) => { if ((_page + 1) * PageSize < _rows.Count) { _page++; UpdatePage(); } };
            toolbar.Controls.Add(_previous);
            toolbar.Controls.Add(_pageLabel);
            toolbar.Controls.Add(_next);
            header.Controls.Add(note);
            header.Controls.Add(toolbar);

            _viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = InputColor };
            _canvas = new Panel { Location = Point.Empty, BackColor = _viewport.BackColor };
            _canvas.Paint += DrawPreview;
            _viewport.Controls.Add(_canvas);
            _viewport.Resize += (sender, args) => UpdateCanvasSize();
            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(_viewport, 0, 1);
            previewPage.Controls.Add(layout);
            commandsPage.Controls.Add(new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
                WordWrap = false, Font = new Font(FontFamily.GenericMonospace, 10), Text = commands,
                BackColor = InputColor, ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.FixedSingle
            });
            tabs.TabPages.Add(previewPage);
            tabs.TabPages.Add(commandsPage);
            Controls.Add(tabs);
            Controls.Add(titleBar);
            UpdatePage();
        }

        private static void StyleButton(Button button, bool primary)
        {
            button.BackColor = primary ? AccentColor : SecondaryButtonColor;
            button.ForeColor = primary ? Color.White : Color.Gainsboro;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.DimGray;
            button.Font = new Font("Segoe UI", 11F, primary ? FontStyle.Bold : FontStyle.Regular);
            button.UseVisualStyleBackColor = false;
        }

        private static void DrawTab(object sender, DrawItemEventArgs e)
        {
            var tabs = (TabControl)sender;
            bool selected = e.Index == tabs.SelectedIndex;
            using (var background = new SolidBrush(selected ? BackgroundColor : HeaderColor))
                e.Graphics.FillRectangle(background, e.Bounds);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds,
                selected ? Color.White : Color.Gainsboro,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (selected)
            {
                using (var accent = new Pen(AccentColor, 3f))
                    e.Graphics.DrawLine(accent, e.Bounds.Left + 8, e.Bounds.Bottom - 2,
                        e.Bounds.Right - 8, e.Bounds.Bottom - 2);
            }
        }

        private int VisibleCount => Math.Min(PageSize, _rows.Count - _page * PageSize);

        private void UpdatePage()
        {
            int first = _page * PageSize + 1;
            _pageLabel.Text = $"Labels {first}–{first + VisibleCount - 1} of {_rows.Count}";
            _previous.Enabled = _page > 0;
            _next.Enabled = (_page + 1) * PageSize < _rows.Count;
            _viewport.AutoScrollPosition = Point.Empty;
            UpdateCanvasSize();
        }

        private void UpdateCanvasSize()
        {
            int width = Math.Max(450, _viewport.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
            _pixelsPerMillimeter = Math.Min(18f,
                Math.Min((width - 70f) / _template.LabelWidth, 260f / _template.LabelLength));
            _labelWidthPixels = _template.LabelWidth * _pixelsPerMillimeter;
            _labelLengthPixels = _template.LabelLength * _pixelsPerMillimeter;
            _gapPixels = _template.LabelGap * _pixelsPerMillimeter;
            _slotHeight = 52 + _labelLengthPixels + _gapPixels;
            _canvas.Size = new Size(width,
                Math.Max(_viewport.ClientSize.Height, (int)Math.Ceiling(12 + VisibleCount * _slotHeight)));
            _canvas.Invalidate();
        }

        private void DrawPreview(object sender, PaintEventArgs e)
        {
            if (_canvas.ClientSize.Width < 100 || _canvas.ClientSize.Height < 20) return;
            Graphics graphics = e.Graphics;
            graphics.Clear(_canvas.BackColor);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            float dotsPerMillimeter = PreviewGeometry.DotsPerMillimeter(PrinterDpi);
            using (var framePen = new Pen(Color.FromArgb(90, 90, 90), 1.5f))
            using (var cardPen = new Pen(Color.FromArgb(128, 130, 165), 1f) { DashStyle = DashStyle.Dash })
            using (var fieldPen = new Pen(Color.SteelBlue, 1f) { DashStyle = DashStyle.Dash })
            using (var outsidePen = new Pen(Color.Firebrick, 2f))
            using (var gapBrush = new HatchBrush(HatchStyle.LightDownwardDiagonal,
                Color.FromArgb(212, 216, 222), Color.FromArgb(244, 245, 247)))
            using (var dotFont = new Font("Arial", Math.Max(8, _template.FontSize), FontStyle.Regular, GraphicsUnit.Pixel))
            using (var previewFont = new Font("Arial",
                Math.Max(1, _template.FontSize * _pixelsPerMillimeter / dotsPerMillimeter),
                FontStyle.Regular, GraphicsUnit.Pixel))
            using (var captionFont = new Font("Segoe UI", 9, FontStyle.Regular))
            {
                for (int local = 0; local < VisibleCount; local++)
                {
                    int index = _page * PageSize + local;
                    float top = 12 + local * _slotHeight;
                    var labelRect = new RectangleF(28, top + 23, _labelWidthPixels, _labelLengthPixels);
                    var gapRect = new RectangleF(labelRect.Left, labelRect.Bottom,
                        labelRect.Width, _gapPixels);
                    graphics.DrawRectangle(cardPen, labelRect.Left - 8, top + 18,
                        labelRect.Width + 16, labelRect.Height + _gapPixels + 16);
                    graphics.FillRectangle(Brushes.White, labelRect);
                    if (_gapPixels > 0) graphics.FillRectangle(gapBrush, gapRect);

                    int fieldsOutside = 0;
                    string[] row = _rows[index];
                    for (int column = 0; column < _template.ColumnCount && column < row.Length; column++)
                    {
                        string value = row[column];
                        if (string.IsNullOrWhiteSpace(value)) continue;
                        SizeF size = graphics.MeasureString(value, dotFont, PointF.Empty,
                            StringFormat.GenericTypographic);
                        RectangleF dotBounds = PreviewGeometry.TextBounds(_template, column, size);
                        bool fits = PreviewGeometry.FitsOnLabel(_template, PrinterDpi, dotBounds);
                        if (!fits) fieldsOutside++;
                        var fieldRect = new RectangleF(
                            labelRect.Left + dotBounds.Left * _pixelsPerMillimeter / dotsPerMillimeter,
                            labelRect.Top + dotBounds.Top * _pixelsPerMillimeter / dotsPerMillimeter,
                            dotBounds.Width * _pixelsPerMillimeter / dotsPerMillimeter,
                            dotBounds.Height * _pixelsPerMillimeter / dotsPerMillimeter);

                        GraphicsState clipped = graphics.Save();
                        graphics.SetClip(labelRect);
                        GraphicsState rotated = graphics.Save();
                        graphics.TranslateTransform(
                            labelRect.Left + _template.XOffsets[column] * _pixelsPerMillimeter / dotsPerMillimeter,
                            labelRect.Top + _template.YOffset * _pixelsPerMillimeter / dotsPerMillimeter);
                        graphics.RotateTransform(LabelRotation.Degrees(_template.Rotation));
                        graphics.DrawString(value, previewFont, Brushes.Black, PointF.Empty,
                            StringFormat.GenericTypographic);
                        graphics.Restore(rotated);
                        graphics.DrawRectangle(fits ? fieldPen : outsidePen,
                            fieldRect.X, fieldRect.Y, fieldRect.Width, fieldRect.Height);
                        graphics.Restore(clipped);
                    }
                    graphics.DrawRectangle(fieldsOutside == 0 ? framePen : outsidePen,
                        labelRect.X, labelRect.Y, labelRect.Width, labelRect.Height);
                    graphics.DrawString($"Label {index + 1}" +
                        (fieldsOutside == 0 ? "" : $"  ·  {fieldsOutside} text field(s) outside label"),
                        captionFont, fieldsOutside == 0 ? Brushes.Gainsboro : Brushes.OrangeRed,
                        labelRect.Left, top + 3);
                    if (_gapPixels >= 17)
                        graphics.DrawString($"Gap {_template.LabelGap} mm", captionFont,
                            Brushes.DimGray, gapRect.Left + 4, gapRect.Top + 1);
                }
            }
        }
    }
}
