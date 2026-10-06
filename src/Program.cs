using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Parcel
{
    static class TabletTheme
    {
        public static readonly Color Background = ColorTranslator.FromHtml("#191d22");
        public static readonly Color Panel = ColorTranslator.FromHtml("#202429");
        public static readonly Color Inset = ColorTranslator.FromHtml("#16191d");
        public static readonly Color Raised = ColorTranslator.FromHtml("#272c32");
        public static readonly Color Hover = ColorTranslator.FromHtml("#30363d");
        public static readonly Color Selected = ColorTranslator.FromHtml("#2c3239");
        public static readonly Color Text = ColorTranslator.FromHtml("#e2e4e8");
        public static readonly Color Muted = ColorTranslator.FromHtml("#a5abb4");
        public static readonly Color Subtle = ColorTranslator.FromHtml("#79828e");
        public static readonly Color Border = ColorTranslator.FromHtml("#383d43");
        public static readonly Color Accent = ColorTranslator.FromHtml("#cbd0d7");
        public static readonly Color AccentHover = ColorTranslator.FromHtml("#e1e5eb");
        public static readonly Color Success = ColorTranslator.FromHtml("#a7c2b5");
        public static readonly Color Eyebrow = ColorTranslator.FromHtml("#929aa5");
        public static GraphicsPath Round(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath(); int diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }
    }
    sealed class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return TabletTheme.Raised; } }
        public override Color ImageMarginGradientBegin { get { return TabletTheme.Raised; } }
        public override Color ImageMarginGradientMiddle { get { return TabletTheme.Raised; } }
        public override Color ImageMarginGradientEnd { get { return TabletTheme.Raised; } }
        public override Color MenuBorder { get { return TabletTheme.Border; } }
        public override Color MenuItemBorder { get { return TabletTheme.Accent; } }
        public override Color MenuItemSelected { get { return TabletTheme.Selected; } }
    }
    sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Padding = new Padding(14, 10, 14, 10);
            BackColor = TabletTheme.Panel;
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? TabletTheme.Background : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var shape = TabletTheme.Round(bounds, 14))
            using (var brush = new SolidBrush(TabletTheme.Panel))
                e.Graphics.FillPath(brush, shape);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var shape = TabletTheme.Round(bounds, 14))
            using (var pen = new Pen(TabletTheme.Border))
                e.Graphics.DrawPath(pen, shape);
        }
    }
    static class Native
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);
        public static void DarkScroll(Control control)
        {
            if (control == null) return;
            Action apply = delegate { try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); } catch (Exception) { } };
            if (control.IsHandleCreated) apply();
            control.HandleCreated += delegate { apply(); };
        }
    }
    sealed class InputFrame : Panel
    {
        readonly Control inner;
        public InputFrame(Control inner, bool multiline)
        {
            this.inner = inner;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Padding = multiline ? new Padding(8, 6, 4, 6) : new Padding(10, 8, 10, 0);
            Dock = multiline ? DockStyle.Fill : DockStyle.None;
            if (!multiline) { Anchor = AnchorStyles.Left | AnchorStyles.Right; Height = 34; Margin = new Padding(3, 0, 3, 0); }
            inner.Dock = DockStyle.Fill;
            Controls.Add(inner);
            inner.Enter += delegate { Invalidate(); }; inner.Leave += delegate { Invalidate(); };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? TabletTheme.Panel : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var shape = TabletTheme.Round(bounds, 8)) using (var brush = new SolidBrush(TabletTheme.Inset)) using (var pen = new Pen(inner.ContainsFocus ? TabletTheme.Accent : TabletTheme.Border)) { e.Graphics.FillPath(brush, shape); e.Graphics.DrawPath(pen, shape); }
        }
    }
    sealed class WheelRedirect : IMessageFilter
    {
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
        readonly Func<Control, bool> accept;
        public WheelRedirect(Func<Control, bool> accept) { this.accept = accept; }
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != 0x20A) return false;
            IntPtr target = WindowFromPoint(Cursor.Position);
            if (target == IntPtr.Zero || target == m.HWnd) return false;
            Control control = Control.FromHandle(target);
            if (control == null || !accept(control)) return false;
            SendMessage(target, m.Msg, m.WParam, m.LParam);
            return true;
        }
    }
    sealed class DarkScrollBar : Control
    {
        int maximum, page, value; bool dragging, hover; int dragOffset;
        public event EventHandler ValueChanged;
        public DarkScrollBar() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); Width = 14; Cursor = Cursors.Default; }
        public void Configure(int maximum, int page, int value)
        {
            this.maximum = Math.Max(0, maximum); this.page = Math.Max(1, page); this.value = Math.Max(0, Math.Min(value, Math.Max(0, this.maximum - this.page)));
            Visible = this.maximum > this.page; Invalidate();
        }
        public int Value { get { return value; } }
        Rectangle Thumb()
        {
            int track = Height - 4; int span = Math.Max(1, maximum);
            int length = Math.Max(28, (int)((long)track * page / span)); length = Math.Min(length, track);
            int range = Math.Max(1, maximum - page);
            int top = 2 + (int)((long)(track - length) * value / range);
            return new Rectangle(3, top, Width - 6, length);
        }
        void SetValue(int next)
        {
            next = Math.Max(0, Math.Min(next, Math.Max(0, maximum - page)));
            if (next == value) return;
            value = next; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? TabletTheme.Panel : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var thumb = Thumb(); if (thumb.Width < 2) return;
            using (var shape = TabletTheme.Round(new Rectangle(thumb.X, thumb.Y, thumb.Width - 1, thumb.Height - 1), Math.Max(1, (thumb.Width - 1) / 2)))
            using (var brush = new SolidBrush(dragging || hover ? TabletTheme.Subtle : TabletTheme.Border)) e.Graphics.FillPath(brush, shape);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            var thumb = Thumb();
            if (thumb.Contains(e.Location)) { dragging = true; dragOffset = e.Y - thumb.Y; Invalidate(); }
            else SetValue(value + (e.Y < thumb.Y ? -page : page));
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) {
                int track = Height - 4, length = Thumb().Height, room = Math.Max(1, track - length);
                SetValue((int)Math.Round((double)(e.Y - dragOffset - 2) * Math.Max(1, maximum - page) / room));
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Invalidate(); base.OnMouseUp(e); }
    }
    sealed class DarkCombo : ComboBox
    {
        [StructLayout(LayoutKind.Sequential)]
        struct PaintStruct { public IntPtr hdc; public bool erase; public int l, t, r, b; public bool restore, incUpdate; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] reserved; }
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr hwnd, out PaintStruct ps);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr hwnd, ref PaintStruct ps);
        public DarkCombo() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnMouseEnter(EventArgs e) { Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Invalidate(); base.OnMouseLeave(e); }
        protected override void WndProc(ref Message m)
        {
            if (DropDownStyle == ComboBoxStyle.DropDownList && IsHandleCreated) {
                if (m.Msg == 0x14) { m.Result = (IntPtr)1; return; }
                if (m.Msg == 0x317 || m.Msg == 0x318) {
                    if (Width > 4 && Height > 4) using (var g = Graphics.FromHdc(m.WParam)) Render(g);
                    m.Result = IntPtr.Zero; return;
                }
                if (m.Msg == 0xF) {
                    PaintStruct ps; IntPtr hdc = BeginPaint(Handle, out ps);
                    try { if (Width > 4 && Height > 4) using (var g = Graphics.FromHdc(hdc)) Render(g); }
                    finally { EndPaint(Handle, ref ps); }
                    m.Result = IntPtr.Zero; return;
                }
            }
            base.WndProc(ref m);
        }
        void Render(Graphics target)
        {
            using (var buffer = new Bitmap(Width, Height))
            using (var g = Graphics.FromImage(buffer)) {
                g.Clear(Parent == null ? TabletTheme.Panel : Parent.BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                bool active = Focused || DroppedDown || ClientRectangle.Contains(PointToClient(Cursor.Position));
                using (var shape = TabletTheme.Round(bounds, 8)) using (var brush = new SolidBrush(TabletTheme.Inset)) using (var pen = new Pen(Focused || DroppedDown ? TabletTheme.Accent : active ? TabletTheme.Subtle : TabletTheme.Border)) { g.FillPath(brush, shape); g.DrawPath(pen, shape); }
                TextRenderer.DrawText(g, Text, Font, new Rectangle(10, 0, Width - 34, Height), TabletTheme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                int cx = Width - 18, cy = Height / 2;
                using (var pen = new Pen(TabletTheme.Muted, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLines(pen, new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
                target.DrawImageUnscaled(buffer, 0, 0);
            }
        }
    }    sealed class BrandTile : Control
    {
        readonly Icon icon;
        public BrandTile() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { } }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            int side = Math.Min(Width, Height) - 1;
            var bounds = new Rectangle(0, 0, side, side);
            using (var shape = TabletTheme.Round(bounds, 14)) using (var brush = new SolidBrush(TabletTheme.Panel)) using (var pen = new Pen(TabletTheme.Border)) { e.Graphics.FillPath(brush, shape); e.Graphics.DrawPath(pen, shape); }
            if (icon != null) e.Graphics.DrawImage(icon.ToBitmap(), new Rectangle(side / 2 - 16, side / 2 - 16, 32, 32));
        }
    }
    sealed class TabletButton : Button
    {
        public bool Primary { get; set; }
        bool hovered;
        public TabletButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Padding = new Padding(14, 5, 14, 5); Cursor = Cursors.Hand; }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Primary && Enabled ? (hovered ? TabletTheme.AccentHover : TabletTheme.Accent) : hovered && Enabled ? TabletTheme.Hover : TabletTheme.Raised;
            using (var shape = TabletTheme.Round(bounds, 8)) using (var brush = new SolidBrush(fill)) using (var pen = new Pen(Focused ? TabletTheme.Accent : TabletTheme.Border)) {
                e.Graphics.FillPath(brush, shape); e.Graphics.DrawPath(pen, shape);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, !Enabled ? TabletTheme.Subtle : Primary ? TabletTheme.Panel : TabletTheme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ReadyForm());
        }
    }
    public sealed class ReadyForm : Form
    {
        readonly TextBox target = new TextBox { ReadOnly = true, BorderStyle = BorderStyle.None };
        readonly TextBox resourceName = new TextBox { BorderStyle = BorderStyle.None };
        readonly ComboBox mode = new DarkCombo { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        readonly TextBox exclusions = Editor();
        readonly TextBox editable = Editor();
        readonly Label status = new Label { Dock = DockStyle.Fill, Text = "Select a resource to get started.", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        readonly Label profile = new Label { Dock = DockStyle.Fill, AutoSize = true, ForeColor = Color.DimGray };
        readonly Button preview = new TabletButton { Text = "Refresh preview", Size = new Size(140, 38), Margin = new Padding(4, 0, 0, 0) };
        readonly Button create = new TabletButton { Text = "Create ZIP", Size = new Size(120, 38), Margin = new Padding(4, 0, 0, 0), Enabled = false, Primary = true };
        readonly Button open = new TabletButton { Text = "Show ZIP in folder", Size = new Size(160, 38), Margin = new Padding(4, 0, 0, 0), Enabled = false };
        readonly Button excludeSelected = new TabletButton { Text = "Exclude selected...", Size = new Size(170, 38), Margin = new Padding(4, 0, 0, 0), Enabled = false };
        readonly ContextMenuStrip excludeMenu = new ContextMenuStrip();
        readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, Visible = false };
        readonly TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(30, 18, 30, 18), BackColor = TabletTheme.Background };
        readonly TextBox saveTarget = new TextBox { ReadOnly = true, BorderStyle = BorderStyle.None };
        const string NoFolderText = "Ask every time (no folder chosen)";
        string saveFolder = "";
        PackageConfig config;
        PackagePlan plan;
        bool loading;
        bool busy;
        string lastZip;
        internal Action<Exception> ErrorReporter { get; set; }
        string Mode { get { return mode.SelectedIndex == 1 ? "source" : "escrow"; } }
        static bool fitting;
        static void FitScrollBars(TextBox box)
        {
            if (fitting || !box.IsHandleCreated || box.Width < 60 || box.Height < 40) return;
            fitting = true;
            try {
                int vbar = SystemInformation.VerticalScrollBarWidth, hbar = SystemInformation.HorizontalScrollBarHeight, pad = 12;
                int widest = box.Lines.Length == 0 ? 0 : box.Lines.Max(l => TextRenderer.MeasureText(l.Length == 0 ? " " : l, box.Font, new Size(int.MaxValue, 100), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width);
                int content = box.Lines.Length * TextRenderer.MeasureText("Ag", box.Font).Height + 4;
                bool needH = widest + pad > box.Width - 2;
                bool needV = content > box.Height - 2 - (needH ? hbar : 0);
                if (needV && widest + pad > box.Width - 2 - vbar) needH = true;
                ScrollBars next = needV && needH ? ScrollBars.Both : needV ? ScrollBars.Vertical : needH ? ScrollBars.Horizontal : ScrollBars.None;
                if (box.ScrollBars != next) box.ScrollBars = next;
            } finally { fitting = false; }
        }        static TextBox Editor() { return new TextBox { Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9) }; }
        public ReadyForm(bool promptForResource = true)
        {
            Text = "Parcel";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(1160, 950);
            MinimumSize = new Size(980, 830);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = TabletTheme.Background;
            ForeColor = TabletTheme.Text;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(layout);
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var tile = new BrandTile { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 12, 2) };
            header.Controls.Add(tile, 0, 0); header.SetRowSpan(tile, 2);
            header.Controls.Add(new Label { Text = "FIVEM RESOURCE  /  PACKAGE BUILDER", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TabletTheme.Eyebrow, AutoSize = true, Margin = new Padding(0, 6, 0, 0) }, 1, 0);
            header.Controls.Add(new Label { Text = "Build a release with confidence", Font = new Font("Segoe UI Semibold", 22), AutoSize = true, Margin = new Padding(0) }, 1, 1);
            Add(header, 74);
            Add(new Label { Text = "Prepare an escrow or source package. Your original resource stays untouched.", ForeColor = TabletTheme.Muted, AutoSize = true }, 30);
            var picker = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            var browse = new TabletButton { Text = "Select manifest", Dock = DockStyle.Fill };
            var folder = new TabletButton { Text = "Select folder", Dock = DockStyle.Fill };
            picker.Controls.Add(new InputFrame(target, false), 0, 0); picker.Controls.Add(browse, 1, 0); picker.Controls.Add(folder, 2, 0);
            AddCard(picker, 68);
            Add(profile, 30);
            var options = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116)); options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            options.Controls.Add(new Label { Text = "Resource name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); options.Controls.Add(new InputFrame(resourceName, false), 1, 0);
            options.Controls.Add(new Label { Text = "Package type", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0); options.Controls.Add(mode, 3, 0);
            mode.Items.AddRange(new object[] { "Escrow", "Source code" }); mode.SelectedIndex = 0;
            AddCard(options, 68);
            var saveRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
            saveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116)); saveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            saveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); saveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            var chooseSave = new TabletButton { Text = "Choose folder", Dock = DockStyle.Fill };
            var clearSave = new TabletButton { Text = "Clear", Dock = DockStyle.Fill };
            saveRow.Controls.Add(new Label { Text = "Save ZIPs to", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); saveRow.Controls.Add(new InputFrame(saveTarget, false), 1, 0);
            saveRow.Controls.Add(chooseSave, 2, 0); saveRow.Controls.Add(clearSave, 3, 0);
            AddCard(saveRow, 68);
            saveFolder = LoadSaveFolder(); saveTarget.Text = saveFolder.Length == 0 ? NoFolderText : saveFolder;
            chooseSave.Click += delegate {
                using (var dialog = new FolderBrowserDialog { Description = "Choose where finished ZIPs are saved. The app remembers this folder.", ShowNewFolderButton = true, SelectedPath = Directory.Exists(saveFolder) ? saveFolder : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) }) {
                    if (dialog.ShowDialog(this) == DialogResult.OK) SetSaveFolder(dialog.SelectedPath);
                }
            };
            clearSave.Click += delegate { SetSaveFolder(""); };
            var rules = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            rules.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); rules.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            rules.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); rules.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rules.Controls.Add(new Label { Text = "EXCLUDE FROM ZIP  /  one pattern per line", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TabletTheme.Eyebrow, AutoSize = true }, 0, 0);
            rules.Controls.Add(new Label { Text = "KEEP EDITABLE IN ESCROW  /  one pattern per line", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TabletTheme.Eyebrow, AutoSize = true }, 1, 0);
            rules.Controls.Add(new InputFrame(exclusions, true), 0, 1); rules.Controls.Add(new InputFrame(editable, true), 1, 1);
            AddCard(rules, 180);
            Add(new Label { Text = "Select files below (right-click or Exclude selected) to exclude a file or its whole folder. Changes apply to this export only.", AutoSize = true, ForeColor = TabletTheme.Muted }, 26);
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Path", HeaderText = "File / folder", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Status", Width = 95 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Reason", HeaderText = "Reason", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45 });
            grid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs e) {
                if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].DataBoundItem is PackageFile) {
                    var file = (PackageFile)grid.Rows[e.RowIndex].DataBoundItem;
                    e.CellStyle.ForeColor = file.Status == "Excluded" ? TabletTheme.Muted : TabletTheme.Text;
                }
            };
            var gridCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            grid.Dock = DockStyle.Fill;
            var gridBar = new DarkScrollBar { Dock = DockStyle.Right, Visible = false };
            grid.ScrollBars = ScrollBars.None;
            gridCard.Controls.Add(grid); gridCard.Controls.Add(gridBar);
            Action syncBar = delegate {
                int first = grid.RowCount > 0 && grid.FirstDisplayedScrollingRowIndex >= 0 ? grid.FirstDisplayedScrollingRowIndex : 0;
                int visible = Math.Max(1, (grid.ClientSize.Height - grid.ColumnHeadersHeight) / Math.Max(1, grid.RowTemplate.Height));
                gridBar.Configure(grid.RowCount, visible, first);
            };
            grid.Scroll += delegate { syncBar(); }; grid.DataSourceChanged += delegate { syncBar(); };
            grid.RowsAdded += delegate { syncBar(); }; grid.RowsRemoved += delegate { syncBar(); }; grid.SizeChanged += delegate { syncBar(); };
            gridBar.ValueChanged += delegate { if (grid.RowCount > 0 && gridBar.Value < grid.RowCount) grid.FirstDisplayedScrollingRowIndex = gridBar.Value; };
            MouseEventHandler wheel = delegate(object sender, MouseEventArgs e) {
                var handled = e as HandledMouseEventArgs; if (handled != null) handled.Handled = true;
                if (grid.RowCount == 0) return;
                int visible = Math.Max(1, (grid.ClientSize.Height - grid.ColumnHeadersHeight) / Math.Max(1, grid.RowTemplate.Height));
                int max = Math.Max(0, grid.RowCount - visible);
                int first = grid.FirstDisplayedScrollingRowIndex >= 0 ? grid.FirstDisplayedScrollingRowIndex : 0;
                int step = Math.Max(1, SystemInformation.MouseWheelScrollLines);
                int next = Math.Max(0, Math.Min(max, first - Math.Sign(e.Delta) * step));
                if (next != first) grid.FirstDisplayedScrollingRowIndex = next;
                syncBar();
            };
            grid.MouseWheel += wheel; gridBar.MouseWheel += wheel; gridCard.MouseWheel += wheel;
            var redirect = new WheelRedirect(c => c == grid || c == gridBar || c == exclusions || c == editable);
            Application.AddMessageFilter(redirect);
            FormClosed += delegate { Application.RemoveMessageFilter(redirect); };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.Controls.Add(gridCard, 0, layout.RowCount++);
            Add(status, 34); Add(progress, 6);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 6, 0, 0) };
            actions.Controls.Add(create); actions.Controls.Add(preview); actions.Controls.Add(open); actions.Controls.Add(excludeSelected);
            Add(actions, 56);
            Add(new Label { Text = "Cfx encrypts after upload. NUI JavaScript remains unprotected. Uses your existing UI build.", AutoSize = true, ForeColor = TabletTheme.Muted }, 22);
            ApplyTheme(layout);
            profile.ForeColor = TabletTheme.Muted;
            grid.BackgroundColor = TabletTheme.Panel;
            grid.GridColor = TabletTheme.Border;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = TabletTheme.Raised, ForeColor = TabletTheme.Muted, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Padding = new Padding(9, 4, 9, 4), SelectionBackColor = TabletTheme.Raised };
            grid.ColumnHeadersHeight = 36;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = TabletTheme.Panel, ForeColor = TabletTheme.Text, SelectionBackColor = TabletTheme.Selected, SelectionForeColor = Color.White, Padding = new Padding(9, 0, 9, 0) };
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(28, 32, 37);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.RowTemplate.Height = 30;
            mode.FlatStyle = FlatStyle.Flat;
            mode.DrawMode = DrawMode.OwnerDrawFixed;
            mode.ItemHeight = 24;
            mode.DrawItem += delegate(object sender, DrawItemEventArgs e) {
                using (var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? TabletTheme.Hover : TabletTheme.Inset)) e.Graphics.FillRectangle(brush, e.Bounds);
                if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, mode.Items[e.Index].ToString(), Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), TabletTheme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            };
            excludeMenu.Renderer = new ToolStripProfessionalRenderer(new MenuColors());
            excludeMenu.BackColor = TabletTheme.Raised; excludeMenu.ForeColor = TabletTheme.Text; excludeMenu.ShowImageMargin = false;
            grid.MouseDown += delegate(object sender, MouseEventArgs e) {
                if (e.Button != MouseButtons.Right) return;
                var hit = grid.HitTest(e.X, e.Y);
                if (hit.RowIndex < 0) return;
                if (!grid.Rows[hit.RowIndex].Selected) { grid.ClearSelection(); grid.Rows[hit.RowIndex].Selected = true; }
                ShowExcludeMenu(grid, e.Location);
            };
            grid.SelectionChanged += delegate { excludeSelected.Enabled = SelectedFiles().Length > 0; };
            excludeSelected.Click += delegate { ShowExcludeMenu(excludeSelected, new Point(0, excludeSelected.Height)); };
            foreach (var box in new[] { exclusions, editable }) {
                var current = box;
                bool queued = false;
                Action schedule = delegate {
                    if (queued || !IsHandleCreated) return;
                    queued = true;
                    BeginInvoke(new Action(delegate { queued = false; FitScrollBars(current); }));
                };
                current.TextChanged += delegate { schedule(); };
                current.SizeChanged += delegate { schedule(); };
                Shown += delegate { schedule(); };
            }
            Native.DarkScroll(exclusions); Native.DarkScroll(editable); Native.DarkScroll(mode);
            browse.Click += delegate { PickManifest(); };
            folder.Click += async delegate {
                using (var dialog = new FolderBrowserDialog { Description = "Select the FiveM resource folder containing fxmanifest.lua", ShowNewFolderButton = false }) {
                    if (dialog.ShowDialog(this) == DialogResult.OK) await LoadResource(dialog.SelectedPath);
                }
            };
            mode.SelectedIndexChanged += async delegate { if (!loading && config != null) { SetRules(); await RefreshPlan(); } };
            resourceName.TextChanged += delegate { InvalidatePlan(); };
            exclusions.TextChanged += delegate { InvalidatePlan(); };
            editable.TextChanged += delegate { InvalidatePlan(); };
            preview.Click += async delegate { await RefreshPlan(); };
            create.Click += async delegate { await Export(); };
            open.Click += delegate { if (lastZip != null) Process.Start("explorer.exe", "/select,\"" + lastZip + "\""); };
            if (promptForResource) Shown += delegate { BeginInvoke(new Action(PickManifest)); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) { e.Cancel = true; status.Text = "Please wait for the current operation to finish."; } };
        }
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4); } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        }
        void ApplyTheme(Control parent)
        {
            foreach (Control control in parent.Controls) {
                if (control is TextBox) { control.BackColor = TabletTheme.Inset; control.ForeColor = TabletTheme.Text; ((TextBox)control).BorderStyle = BorderStyle.None; }
                else if (control is ComboBox) { control.BackColor = TabletTheme.Inset; control.ForeColor = TabletTheme.Text; }
                else if (control is Button) { control.BackColor = TabletTheme.Raised; control.ForeColor = TabletTheme.Text; }
                ApplyTheme(control);
            }
        }
        void Add(Control control, int height) { control.Dock = DockStyle.Fill; layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); layout.Controls.Add(control, 0, layout.RowCount++); }
        void AddCard(Control control, int height)
        {
            var card = new CardPanel { Dock = DockStyle.Fill };
            control.Dock = DockStyle.Fill;
            card.Controls.Add(control);
            Add(card, height);
        }
        string[] SelectedFiles()
        {
            return grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as PackageFile)
                .Where(f => f != null && f.Status == "Included").Select(f => f.Path).ToArray();
        }
        void ShowExcludeMenu(Control owner, Point location)
        {
            string[] files = SelectedFiles();
            if (files.Length == 0) return;
            string[] folders = files.Where(p => p.Contains("/")).Select(p => p.Substring(0, p.LastIndexOf('/'))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            excludeMenu.Items.Clear();
            excludeMenu.Items.Add(files.Length == 1 ? "Exclude only this file" : "Exclude only these " + files.Length + " files", null, async delegate { await AddExclusions(files); });
            var folderItem = excludeMenu.Items.Add(folders.Length == 1 ? "Exclude entire folder: " + folders[0] + "/" : folders.Length == 0 ? "Exclude entire folder (files are in the root)" : "Exclude " + folders.Length + " entire folders", null, async delegate { await AddExclusions(folders); });
            folderItem.Enabled = folders.Length > 0;
            excludeMenu.Show(owner, location);
        }
        async Task AddExclusions(string[] patterns)
        {
            var current = Lines(exclusions).ToList();
            string[] added = patterns.Where(p => !current.Contains(p, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (added.Length == 0) return;
            exclusions.Lines = current.Concat(added).ToArray();
            await RefreshPlan();
        }
        async void PickManifest()
        {
            using (var dialog = new OpenFileDialog { Title = "Choose your resource's fxmanifest.lua", Filter = "FiveM resource manifest (fxmanifest.lua)|fxmanifest.lua", CheckFileExists = true }) {
                if (dialog.ShowDialog(this) == DialogResult.OK) await LoadResource(Path.GetDirectoryName(dialog.FileName));
            }
        }
        async Task LoadResource(string root)
        {
            try {
                var next = PackageEngine.Load(root);
                loading = true; target.Text = root; config = next; resourceName.Text = config.resourceName;
                profile.Text = File.Exists(Path.Combine(root, "packaging.json")) ? "Rules loaded from packaging.json. Changes below apply to this export only." : "Default rules loaded. Review exclusions for this resource before exporting.";
                SetRules(); loading = false; await RefreshPlan();
            } catch (Exception error) { loading = false; ShowError(error); }
        }
        void SetRules()
        {
            loading = true;
            exclusions.Lines = PackageEngine.OrEmpty(config.commonExclude).Concat(PackageEngine.OrEmpty(config.modes[Mode].exclude)).Distinct().ToArray();
            editable.Lines = Mode == "source" ? new[] { "*", "**/*" } : PackageEngine.OrEmpty(config.modes[Mode].escrowIgnore);
            editable.ReadOnly = Mode == "source";
            loading = false; InvalidatePlan();
        }
        void InvalidatePlan() { if (loading) return; plan = null; create.Enabled = false; status.Text = "Rules changed. Refresh the preview to continue."; }
        string[] Lines(TextBox box) { return box.Lines.Select(s => s.Trim()).Where(s => s.Length > 0).ToArray(); }
        void Busy(bool value) { busy = value; layout.Enabled = !value; progress.Visible = value; UseWaitCursor = value; }
        void ShowError(Exception error) { status.Text = "Unable to complete the operation."; if (ErrorReporter != null) ErrorReporter(error); else MessageBox.Show(this, error.Message, "Parcel", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        async Task RefreshPlan()
        {
            if (config == null) { PickManifest(); return; }
            plan = null; create.Enabled = false; Busy(true); status.Text = "Scanning resource...";
            try {
                string root = target.Text, name = resourceName.Text.Trim(), selectedMode = Mode;
                string[] excluded = Lines(exclusions), ignored = Lines(editable);
                plan = await Task.Run(() => PackageEngine.Scan(root, name, selectedMode, config, excluded, ignored));
                grid.DataSource = plan.Files;
                int count = plan.Files.Count(f => f.Status == "Included");
                double size = plan.Files.Where(f => f.Status == "Included").Sum(f => f.Size) / 1048576.0;
                status.Text = string.Format("{0} files included  |  {1} files / folders excluded  |  {2:0.0} MB before compression", count, plan.Files.Count - count, size);
                create.Enabled = true;
            } catch (Exception error) { grid.DataSource = null; ShowError(error); }
            finally { Busy(false); }
        }
        async Task Export()
        {
            if (plan == null) return;
            var snapshot = plan;
            string fileName = snapshot.Name + "-" + snapshot.Mode + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string destination = null;
            if (!string.IsNullOrEmpty(saveFolder) && Directory.Exists(saveFolder)) {
                destination = Path.Combine(saveFolder, fileName + ".zip");
                for (int i = 2; File.Exists(destination); i++) destination = Path.Combine(saveFolder, fileName + "-" + i + ".zip");
            } else {
                using (var dialog = new SaveFileDialog { Title = "Save resource ZIP", Filter = "ZIP archive|*.zip", DefaultExt = "zip", AddExtension = true,
                    FileName = fileName + ".zip", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), OverwritePrompt = false }) {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    destination = dialog.FileName;
                }
                if (File.Exists(destination)) { MessageBox.Show(this, "Choose a new file name. Existing ZIPs are not overwritten.", Text); return; }
            }
            Busy(true); status.Text = "Creating ZIP...";
            try {
                await Task.Run(() => PackageEngine.Create(snapshot, destination));
                lastZip = destination; open.Enabled = true;
                status.Text = "ZIP ready: " + lastZip;
                MessageBox.Show(this, "Your " + snapshot.Mode + " package is ready.\n\n" + lastZip + "\n\nUpload the ZIP through the Cfx Assets portal.", "Package complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception error) { ShowError(error); }
            finally { Busy(false); }
        }
        static string SettingsFile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Parcel", "settings.txt"); } }
        static string LoadSaveFolder()
        {
            try {
                if (!File.Exists(SettingsFile)) { string old = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(SettingsFile)), "FivemResourceReady", "settings.txt"); if (!File.Exists(old)) return ""; try { Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)); File.Copy(old, SettingsFile); } catch { return ""; } }
                foreach (string line in File.ReadAllLines(SettingsFile)) if (line.StartsWith("saveFolder=")) return line.Substring(11).Trim();
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return "";
        }
        void SetSaveFolder(string folder)
        {
            saveFolder = folder ?? "";
            saveTarget.Text = saveFolder.Length == 0 ? NoFolderText : saveFolder;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
                File.WriteAllText(SettingsFile, "saveFolder=" + saveFolder + Environment.NewLine);
            } catch (IOException) { status.Text = "Save folder set, but it could not be remembered."; } catch (UnauthorizedAccessException) { status.Text = "Save folder set, but it could not be remembered."; }
        }    }
}













