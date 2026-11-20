using System.Drawing.Text;

namespace FangKeDelivery.UI;

/// <summary>统一的配色、字体与控件工厂，保证界面风格一致。</summary>
public static class Theme
{
    public static readonly Color Primary = Color.FromArgb(21, 101, 192);
    public static readonly Color PrimaryDark = Color.FromArgb(13, 71, 161);
    public static readonly Color PrimaryLight = Color.FromArgb(232, 240, 254);
    public static readonly Color Accent = Color.FromArgb(46, 160, 67);
    public static readonly Color Danger = Color.FromArgb(200, 55, 55);
    public static readonly Color Warn = Color.FromArgb(230, 145, 20);
    public static readonly Color PageBack = Color.FromArgb(244, 246, 249);
    public static readonly Color CardBack = Color.White;
    public static readonly Color Border = Color.FromArgb(214, 220, 228);
    public static readonly Color TextMain = Color.FromArgb(33, 37, 41);
    public static readonly Color TextSub = Color.FromArgb(110, 119, 129);
    public static readonly Color GridHeaderBack = Color.FromArgb(236, 241, 248);
    public static readonly Color GridAltRow = Color.FromArgb(249, 251, 253);

    public static readonly string FontFamily = PickFont();

    private static string PickFont()
    {
        var wanted = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑", "SimSun", "Segoe UI" };
        var installed = FontFamilyNames();
        foreach (var w in wanted)
        {
            if (installed.Contains(w))
                return w;
        }
        return SystemFonts.DefaultFont.FontFamily.Name;
    }

    private static HashSet<string> FontFamilyNames()
    {
        try
        {
            using var fonts = new InstalledFontCollection();
            return fonts.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static Font Font(float size, FontStyle style = FontStyle.Regular) =>
        new(FontFamily, size, style, GraphicsUnit.Point);

    public static Font TitleFont => Font(16f, FontStyle.Bold);
    public static Font SubTitleFont => Font(11f, FontStyle.Bold);
    public static Font BodyFont => Font(9.5f);
    public static Font SmallFont => Font(8.5f);

    public static Button PrimaryButton(string text, int width = 96, int height = 34)
    {
        var b = BaseButton(text, width, height);
        b.BackColor = Primary;
        b.ForeColor = Color.White;
        b.FlatAppearance.MouseOverBackColor = PrimaryDark;
        b.FlatAppearance.MouseDownBackColor = PrimaryDark;
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    public static Button SecondaryButton(string text, int width = 96, int height = 34)
    {
        var b = BaseButton(text, width, height);
        b.BackColor = Color.White;
        b.ForeColor = TextMain;
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = PrimaryLight;
        return b;
    }

    public static Button DangerButton(string text, int width = 96, int height = 34)
    {
        var b = BaseButton(text, width, height);
        b.BackColor = Color.White;
        b.ForeColor = Danger;
        b.FlatAppearance.BorderColor = Color.FromArgb(232, 190, 190);
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(253, 240, 240);
        return b;
    }

    private static Button BaseButton(string text, int width, int height) => new()
    {
        Text = text,
        Width = width,
        Height = height,
        FlatStyle = FlatStyle.Flat,
        Font = Font(9.5f),
        Cursor = Cursors.Hand,
        UseVisualStyleBackColor = false,
        TextAlign = ContentAlignment.MiddleCenter
    };

    public static TextBox Input(int width = 200) => new()
    {
        Width = width,
        Font = BodyFont,
        BorderStyle = BorderStyle.FixedSingle
    };

    public static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Width = 84,
        Height = 26,
        TextAlign = ContentAlignment.MiddleRight,
        ForeColor = TextSub,
        Font = BodyFont
    };

    public static Label SectionTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Font(11f, FontStyle.Bold),
        ForeColor = TextMain,
        Margin = new Padding(0, 0, 0, 8)
    };

    public static void StyleGrid(DataGridView g)
    {
        g.BackgroundColor = Color.White;
        g.BorderStyle = BorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.GridColor = Color.FromArgb(235, 238, 242);
        g.EnableHeadersVisualStyles = false;
        g.RowHeadersVisible = false;
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.AllowUserToResizeRows = false;
        g.MultiSelect = true;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.ColumnHeadersHeight = 36;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.RowTemplate.Height = 32;
        g.Font = BodyFont;
        g.ColumnHeadersDefaultCellStyle.BackColor = GridHeaderBack;
        g.ColumnHeadersDefaultCellStyle.ForeColor = TextMain;
        g.ColumnHeadersDefaultCellStyle.Font = Font(9.5f, FontStyle.Bold);
        g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        g.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        g.DefaultCellStyle.BackColor = Color.White;
        g.DefaultCellStyle.ForeColor = TextMain;
        g.DefaultCellStyle.SelectionBackColor = PrimaryLight;
        g.DefaultCellStyle.SelectionForeColor = TextMain;
        g.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        g.AlternatingRowsDefaultCellStyle.BackColor = GridAltRow;
        g.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
        g.AdvancedCellBorderStyle.Bottom = DataGridViewAdvancedCellBorderStyle.Single;
        g.AdvancedCellBorderStyle.Top = DataGridViewAdvancedCellBorderStyle.Single;
        g.AdvancedCellBorderStyle.Left = DataGridViewAdvancedCellBorderStyle.Single;
        g.AdvancedCellBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.Single;
        g.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
        g.AdvancedColumnHeadersBorderStyle.Bottom = DataGridViewAdvancedCellBorderStyle.Single;
    }

    public static Panel Card(Control? child = null, Padding? padding = null)
    {
        var p = new Panel
        {
            BackColor = CardBack,
            Padding = padding ?? new Padding(16),
            Dock = DockStyle.Fill
        };
        if (child is not null)
            p.Controls.Add(child);
        return p;
    }

    /// <summary>带圆角与细边框的卡片容器（自绘）。</summary>
    public sealed class RoundedPanel : Panel
    {
        public int Radius { get; set; } = 8;
        public Color BorderColor { get; set; } = Theme.Border;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundRect(rect, Radius);
            using var fill = new SolidBrush(BackColor);
            g.FillPath(fill, path);
            using var pen = new Pen(BorderColor);
            g.DrawPath(pen, path);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
