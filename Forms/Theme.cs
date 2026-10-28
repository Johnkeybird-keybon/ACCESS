using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace AccessSystem.Forms;

/// <summary>Colors, fonts and small helpers taken from the Figma design.</summary>
public static class Theme
{
    // ----- palette (sampled from the Figma frames) -----
    public static readonly Color HeaderBg  = Color.FromArgb(0xF6, 0xF5, 0xEC); // header + sidebar
    public static readonly Color Content   = Color.FromArgb(0xED, 0xEB, 0xDA); // page background
    public static readonly Color Brown     = Color.FromArgb(0x67, 0x54, 0x48); // text, banner, active nav
    public static readonly Color BrownHover= Color.FromArgb(0x7A, 0x66, 0x59);
    public static readonly Color Tan       = Color.FromArgb(0xB2, 0x90, 0x79); // buttons + card borders
    public static readonly Color TanHover  = Color.FromArgb(0xA0, 0x7E, 0x68);
    public static readonly Color Card      = Color.FromArgb(0xEF, 0xE7, 0xDA);
    public static readonly Color TableHead = Color.FromArgb(0xE8, 0xE0, 0xD2);
    public static readonly Color Field     = Color.FromArgb(0xFD, 0xFC, 0xFA);
    public static readonly Color NavHover  = Color.FromArgb(0xEA, 0xE5, 0xD5);
    public static readonly Color Selection = Color.FromArgb(0xE2, 0xD3, 0xC4);
    public static readonly Color Red       = Color.FromArgb(0xC8, 0x34, 0x0F);
    public static readonly Color DueText   = Color.FromArgb(0xB0, 0x3A, 0x0A);
    public static readonly Color Watermark = Color.FromArgb(0xDC, 0xD9, 0xC6);

    // ----- fonts -----
    // Bahnschrift ships with Windows 10/11 and is the closest built-in match to the condensed
    // headings in the design. If it isn't installed we fall back to Segoe UI.
    static readonly HashSet<string> installed =
        new InstalledFontCollection().Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    static string Pick(params string[] names) => names.FirstOrDefault(installed.Contains) ?? "Segoe UI";

    static readonly string displayFamily =
        Pick("Bahnschrift SemiBold Condensed", "Bahnschrift Condensed", "Bahnschrift", "Segoe UI Semibold");
    static readonly string bodyFamily = Pick("Lexend", "Segoe UI");

    public static Font Display(float pt, FontStyle style = FontStyle.Regular) => new(displayFamily, pt, style);
    public static Font Body(float pt, FontStyle style = FontStyle.Regular) => new(bodyFamily, pt, style);

    // ----- helpers -----
    /// <summary>Loads Assets/&lt;file&gt; next to the exe (returns null if missing, so the UI still works).</summary>
    public static Image? Asset(string file)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", file);
            if (!File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            using var tmp = Image.FromStream(fs);
            return new Bitmap(tmp);          // copy so the file isn't locked
        }
        catch { return null; }
    }

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>First opaque BackColor going up the parent chain.</summary>
    public static Color BackdropOf(Control? c)
    {
        while (c != null)
        {
            if (c.BackColor.A == 255) return c.BackColor;
            c = c.Parent;
        }
        return Content;
    }
}

/// <summary>Panel with a rounded, bordered fill (the cards in the design).</summary>
public class RoundedPanel : Panel
{
    public Color FillColor { get; set; } = Theme.Card;
    public Color BorderColor { get; set; } = Theme.Tan;
    public int Radius { get; set; } = 28;
    public float BorderWidth { get; set; } = 1.5f;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Content;      // shows in the corners
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(BorderWidth / 2, BorderWidth / 2, Width - BorderWidth - 1, Height - BorderWidth - 1);
        using var path = Theme.RoundRect(r, Radius);
        using (var fill = new SolidBrush(FillColor)) g.FillPath(fill, path);
        if (BorderWidth > 0)
        {
            using var pen = new Pen(BorderColor, BorderWidth);
            g.DrawPath(pen, path);
        }
    }
}

/// <summary>Rounded button. Also used for the sidebar navigation (Ghost = no fill until active/hover).</summary>
public class RoundButton : Button
{
    public Color Fill { get; set; } = Theme.Tan;
    public Color HoverFill { get; set; } = Theme.TanHover;
    public Color TextColor { get; set; } = Color.White;
    public Color? Backdrop { get; set; }          // color behind the rounded corners
    public Color? Outline { get; set; }
    public int Radius { get; set; } = 18;
    public bool PlusIcon { get; set; }
    public bool Ghost { get; set; }
    public bool Active { get; set; }
    bool hover;

    public RoundButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Font = Theme.Body(11, FontStyle.Bold);
        UseVisualStyleBackColor = false;
    }

    protected override bool ShowFocusCues => false;
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaintBackground(PaintEventArgs pevent) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Backdrop ?? Theme.BackdropOf(Parent));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
        using var path = Theme.RoundRect(rect, Radius);

        Color? fill = Ghost
            ? (Active ? Fill : hover ? Theme.NavHover : null)
            : (hover ? HoverFill : Fill);
        if (fill is { } f)
        {
            using var br = new SolidBrush(f);
            g.FillPath(br, path);
        }
        if (Outline is { } o)
        {
            using var pen = new Pen(o, 2f);
            g.DrawPath(pen, path);
        }

        var color = Ghost && !Active ? Theme.Brown : TextColor;
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
        var area = ClientRectangle;

        if (PlusIcon)
        {
            var size = TextRenderer.MeasureText(Text, Font);
            const int icon = 30, gap = 10;
            int total = icon + gap + size.Width;
            int x0 = (Width - total) / 2;
            int y0 = (Height - icon) / 2;
            using var pen = new Pen(color, 2f);
            g.DrawEllipse(pen, x0, y0, icon, icon);
            g.DrawLine(pen, x0 + icon / 2, y0 + 8, x0 + icon / 2, y0 + icon - 8);
            g.DrawLine(pen, x0 + 8, y0 + icon / 2, x0 + icon - 8, y0 + icon / 2);
            area = new Rectangle(x0 + icon + gap, 0, size.Width + 4, Height);
            flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        }

        TextRenderer.DrawText(g, Text, Font, area, color, flags);
    }
}

/// <summary>Pill-shaped search field with a magnifier icon.</summary>
public class SearchBox : RoundedPanel
{
    public readonly TextBox Input = new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Card,
        ForeColor = Theme.Brown,
        Font = Theme.Body(12)
    };

    public SearchBox(string placeholder = "")
    {
        FillColor = Theme.Card;
        BorderColor = Theme.Brown;
        BorderWidth = 2f;
        Radius = 26;
        Height = 52;
        Input.PlaceholderText = placeholder;
        Controls.Add(Input);
    }

    public override string Text { get => Input.Text; set => Input.Text = value; }

    public event EventHandler? SearchChanged
    {
        add => Input.TextChanged += value;
        remove => Input.TextChanged -= value;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Input.SetBounds(60, (Height - Input.Height) / 2, Math.Max(20, Width - 60 - 26), Input.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Theme.Brown, 3f) { EndCap = LineCap.Round, StartCap = LineCap.Round };
        int cx = 22, cy = Height / 2 - 4;
        g.DrawEllipse(pen, cx, cy - 8, 16, 16);
        g.DrawLine(pen, cx + 14, cy + 6, cx + 21, cy + 13);
    }
}

/// <summary>White rounded input box that hosts a TextBox / ComboBox / NumericUpDown / DateTimePicker.</summary>
public class Field : RoundedPanel
{
    readonly Control inner;
    readonly bool fillHeight;

    public Field(Control inner, int height = 50, bool fillHeight = false)
    {
        this.inner = inner;
        this.fillHeight = fillHeight;
        FillColor = Theme.Field;
        BorderColor = Theme.Brown;
        BorderWidth = 2f;
        Radius = 14;
        BackColor = Color.Transparent;
        if (!fillHeight) Height = height;

        inner.Font = Theme.Body(12);
        inner.ForeColor = Theme.Brown;
        inner.BackColor = Theme.Field;
        switch (inner)
        {
            case TextBox t: t.BorderStyle = BorderStyle.None; break;
            case ComboBox c: c.FlatStyle = FlatStyle.Flat; break;
            case NumericUpDown n: n.BorderStyle = BorderStyle.None; break;
        }
        Controls.Add(inner);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (fillHeight)
            inner.SetBounds(14, 10, Math.Max(20, Width - 28), Math.Max(20, Height - 20));
        else
            inner.SetBounds(14, (Height - inner.Height) / 2, Math.Max(20, Width - 28), inner.Height);
    }
}

/// <summary>Dashboard stat card: title, big number, small caption.</summary>
public class StatCard : RoundedPanel
{
    static readonly Font titleFont = Theme.Body(15);
    static readonly Font valueFont = Theme.Display(46);
    static readonly Font subFont = Theme.Body(13);

    string title = "", value = "0", sub = "";
    public Color ValueColor { get; set; } = Theme.Brown;
    public Color SubColor { get; set; } = Theme.Brown;

    public StatCard(string title)
    {
        this.title = title;
        Radius = 36;
    }

    public void Set(string newValue, string newSub)
    {
        value = newValue; sub = newSub;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        TextRenderer.DrawText(g, title, titleFont, new Point(24, 16), Theme.Brown, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, value, valueFont, new Point(22, 44), ValueColor, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, sub, subFont, new Point(24, Height - 40), SubColor, TextFormatFlags.NoPadding);
    }
}

/// <summary>Content page: cream background + the faint ACCESS watermark in the bottom-right corner.</summary>
public class Page : Panel
{
    static readonly Font wmBig = Theme.Display(44, FontStyle.Italic);
    static readonly Font wmSmall = Theme.Display(15, FontStyle.Italic);

    public Page()
    {
        DoubleBuffered = true;
        BackColor = Theme.Content;
        Dock = DockStyle.Fill;
        Padding = new Padding(40, 34, 40, 100);   // bottom space is where the watermark sits
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var right = TextFormatFlags.Right | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(e.Graphics, "ACCESS", wmBig,
            new Rectangle(0, Height - 100, Width - 26, 52), Theme.Watermark, right);
        TextRenderer.DrawText(e.Graphics, "(Asset Control, Checkout, and Equipment Supervision System)", wmSmall,
            new Rectangle(0, Height - 46, Width - 26, 28), Theme.Watermark, right);
    }
}
