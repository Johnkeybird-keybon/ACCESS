using AccessSystem.Services;

namespace AccessSystem.Forms;

/// <summary>Rows that carry a status text ("Active" | "Overdue" | "Returned") so grids can colour them.</summary>
public interface IStatusRow { string Status { get; } }

/// <summary>Small helpers so the forms stay short.</summary>
public static class Ui
{
    static readonly Font actionFont = Theme.Body(9.5f, FontStyle.Bold);

    // ================= Grid =================
    /// <param name="plainHeader">Header blends into the card (used on the dashboard list).</param>
    public static DataGridView NewGrid(bool plainHeader = false)
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BackgroundColor = Theme.Card,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.None,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 52
        };
        g.RowTemplate.Height = 58;

        var head = plainHeader ? Theme.Card : Theme.TableHead;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = head, ForeColor = Theme.Brown, SelectionBackColor = head, SelectionForeColor = Theme.Brown,
            Font = Theme.Body(11, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleCenter
        };
        g.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Card, ForeColor = Theme.Brown,
            SelectionBackColor = Theme.Selection, SelectionForeColor = Theme.Brown,
            Font = Theme.Body(11), Alignment = DataGridViewContentAlignment.MiddleCenter,
            WrapMode = DataGridViewTriState.True, Padding = new Padding(4, 2, 4, 2)
        };

        HookActionColumns(g);
        return g;
    }

    /// <summary>Bind data, hide technical columns, format dates, keep button columns last.</summary>
    public static void Bind(DataGridView g, object data, params string[] hide)
    {
        g.DataSource = data;
        foreach (DataGridViewColumn c in g.Columns)
        {
            if (IsAction(c)) continue;
            c.Visible = !hide.Contains(c.Name);
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            if (c.ValueType == typeof(DateTime) || c.ValueType == typeof(DateTime?))
            {
                c.DefaultCellStyle.Format = "MMM dd, yyyy hh:mm tt";
                c.DefaultCellStyle.NullValue = "—";
            }
        }
        foreach (var c in g.Columns.Cast<DataGridViewColumn>().Where(IsAction).ToList())
            c.DisplayIndex = g.Columns.Count - 1;
    }

    /// <summary>Rename headers: Headers(g, "Qty=Available / Total Qty", ...).</summary>
    public static void Headers(DataGridView g, params string[] map)
    {
        foreach (var m in map)
        {
            int i = m.IndexOf('=');
            if (i > 0 && g.Columns.Contains(m[..i])) g.Columns[m[..i]]!.HeaderText = m[(i + 1)..];
        }
    }

    // ----- rounded tan buttons inside grid rows (Edit / Archive / Mark as Returned ...) -----
    static bool IsAction(DataGridViewColumn c) => c.Tag is Func<object, string?>;

    /// <summary>Adds a button column. label(row) returns the button text, or null for no button.</summary>
    public static void AddAction(DataGridView g, string name, Func<object, string?> label, int width = 92)
    {
        g.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name, HeaderText = "", Width = width, MinimumWidth = width,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable, Tag = label
        });
    }

    static void HookActionColumns(DataGridView g)
    {
        g.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (g.Columns[e.ColumnIndex].Tag is not Func<object, string?> label) return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
            e.Handled = true;
            if (g.Rows[e.RowIndex].DataBoundItem is not { } item) return;
            var text = label(item);
            if (string.IsNullOrEmpty(text) || e.Graphics == null) return;

            int w = Math.Min(e.CellBounds.Width - 10, 100);
            int h = text.Length > 9 ? 46 : 34;
            var rect = new Rectangle(e.CellBounds.X + (e.CellBounds.Width - w) / 2,
                                     e.CellBounds.Y + (e.CellBounds.Height - h) / 2, w, h);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = Theme.RoundRect(rect, 12);
            using var br = new SolidBrush(Theme.Tan);
            e.Graphics.FillPath(br, path);
            TextRenderer.DrawText(e.Graphics, text, actionFont, rect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        };

        g.CellMouseEnter += (_, e) =>
            g.Cursor = e.RowIndex >= 0 && e.ColumnIndex >= 0 && IsAction(g.Columns[e.ColumnIndex])
                ? Cursors.Hand : Cursors.Default;
        g.CellMouseLeave += (_, _) => g.Cursor = Cursors.Default;
    }

    /// <summary>Which action column was clicked (or null).</summary>
    public static string? ActionClicked(DataGridView g, DataGridViewCellEventArgs e) =>
        e.RowIndex >= 0 && e.ColumnIndex >= 0 && IsAction(g.Columns[e.ColumnIndex])
            ? g.Columns[e.ColumnIndex].Name : null;

    /// <summary>Red text for overdue, grey for returned (rows that implement IStatusRow).</summary>
    public static void ColorStatusRows(DataGridView g) =>
        g.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= g.Rows.Count) return;
            if (g.Rows[e.RowIndex].DataBoundItem is not IStatusRow r) return;
            if (r.Status == "Overdue") e.CellStyle.ForeColor = Color.Firebrick;
            else if (r.Status == "Returned") e.CellStyle.ForeColor = Color.Gray;
        };

    // ================= Small widgets =================
    public static RoundButton Btn(string text, Action onClick)
    {
        var b = new RoundButton { Text = text, AutoSize = false, Size = new Size(150, 46) };
        b.Click += (_, _) => onClick();
        return b;
    }

    public static Label Title(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Theme.Display(34),
        ForeColor = Theme.Brown,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 14)
    };

    /// <summary>Label above a rounded input, like the Borrow form in the design.</summary>
    public static Control Labeled(string label, Control input, bool required = false,
                                  int fieldHeight = 50, bool fill = false, Color? labelColor = null)
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 10)
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(fill ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, fieldHeight));

        var lbl = new Label
        {
            Text = label + (required ? " *" : ""),
            AutoSize = true,
            Font = Theme.Body(11.5f, FontStyle.Bold),
            ForeColor = labelColor ?? Theme.Brown,
            BackColor = Color.Transparent,
            Margin = new Padding(2, 0, 0, 4)
        };
        var f = new Field(input, fieldHeight, fill) { Dock = DockStyle.Fill, Margin = Padding.Empty };
        t.Controls.Add(lbl, 0, 0);
        t.Controls.Add(f, 0, 1);
        return t;
    }

    /// <summary>Two-row layout: a bar on top, content filling the rest (optional bar at the bottom).</summary>
    public static TableLayoutPanel Stack(Control top, Control fill, Control? bottom = null)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        top.Dock = DockStyle.Fill; fill.Dock = DockStyle.Fill;
        t.Controls.Add(top, 0, 0);
        t.Controls.Add(fill, 0, 1);
        if (bottom != null)
        {
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.Dock = DockStyle.Fill;
            t.Controls.Add(bottom, 0, 2);
        }
        t.RowCount = bottom == null ? 2 : 3;
        return t;
    }

    // ----- drag & drop helpers (no longer used by the new screens, kept in case you want them back) -----
    // Payloads are plain strings like "equip:12" or "borrow:5" (safe to pass between controls).

    /// <summary>Lets the user drag a grid row; payload = prefix:id.</summary>
    public static void EnableRowDrag(DataGridView g, string prefix, Func<object, int> idOf)
    {
        int row = -1;
        Point start = Point.Empty;

        g.MouseDown += (_, e) =>
        {
            row = e.Button == MouseButtons.Left ? g.HitTest(e.X, e.Y).RowIndex : -1;
            start = e.Location;
        };
        g.MouseUp += (_, _) => row = -1;
        g.MouseMove += (_, e) =>
        {
            if (row < 0 || e.Button != MouseButtons.Left) return;
            var d = SystemInformation.DragSize;
            if (Math.Abs(e.X - start.X) < d.Width && Math.Abs(e.Y - start.Y) < d.Height) return;

            int r = row;
            row = -1;
            if (r >= g.Rows.Count || g.Rows[r].DataBoundItem is not { } item) return;
            g.DoDragDrop($"{prefix}:{idOf(item)}", DragDropEffects.Copy);
        };
    }

    /// <summary>True if the drag carries our "prefix:id" payload.</summary>
    public static bool TryGetDrop(DragEventArgs e, string prefix, out int id)
    {
        id = 0;
        return e.Data?.GetData(DataFormats.StringFormat) is string s
            && s.StartsWith(prefix + ":")
            && int.TryParse(s[(prefix.Length + 1)..], out id);
    }
}
