using System.ComponentModel;
using AccessSystem.Services;

namespace AccessSystem.Forms;

// ----- row shapes shown in the grids (property names are the column names) -----
record DashRow(string Borrower, string Item, int Quantity, string Date, string Status) : IStatusRow;
record InvRow(EquipmentRow Src, string Item, string Brand, string Category, string Qty, string Status);
record LogRow(int Id, string Borrower, string Item, string Room, string Time, string Status) : IStatusRow;
record HistRow(int Id, string Borrower, string Role, string Item, string BorrowedAt, string ReturnedAt, string Status) : IStatusRow;
record ItemChoice(int Id, string Label);

/// <summary>
/// Main window: header + sidebar + one "page" at a time, styled after the Figma frames
/// (Dashboard, Inventory, Borrow, Borrowing &amp; Return Logs, Borrower History Log).
/// </summary>
public class MainForm : Form
{
    /// <summary>True when the user clicked Logout (Program then shows the login again).</summary>
    public bool LoggedOut { get; private set; }

    // One line in the borrow cart. Quantity can't go below 1 or above what's available.
    sealed class CartLine
    {
        public int EquipmentId { get; init; }
        public string Equipment { get; init; } = "";
        public int Available { get; init; }
        int qty = 1;
        public int Quantity { get => qty; set => qty = Math.Clamp(value, 1, Math.Max(1, Available)); }
    }

    // ----- shell -----
    readonly Panel host = new() { Dock = DockStyle.Fill, BackColor = Theme.Content };
    readonly FlowLayoutPanel nav = new()
    {
        Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.HeaderBg
    };
    readonly Dictionary<string, (Control Page, RoundButton Nav, Action Load)> screens = new();

    // ----- Dashboard -----
    readonly StatCard cardTotal = new("Total Items");
    readonly StatCard cardActive = new("Active Borrowed Items");
    readonly DataGridView dgvDashActive = Ui.NewGrid(plainHeader: true);

    // ----- Inventory -----
    readonly SearchBox searchInv = new("Search name, brand or category");
    readonly CheckBox chkArchived = new() { Text = "Show archived", AutoSize = true };
    readonly DataGridView dgvInventory = Ui.NewGrid();

    // ----- Borrow -----
    readonly TextBox txtBorrower = new() { MaxLength = 120 };
    readonly ComboBox cboBorrowerRole = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox txtRoom = new() { MaxLength = 100 };
    readonly TextBox txtPurpose = new() { MaxLength = 500, Multiline = true, ScrollBars = ScrollBars.Vertical };
    readonly DateTimePicker dtpReturn = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMM dd, yyyy  hh:mm tt" };
    readonly ComboBox cboEquipment = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly NumericUpDown nudQty = new() { Minimum = 1, Maximum = 1, Value = 1 };
    readonly DataGridView dgvCart = Ui.NewGrid();
    readonly BindingList<CartLine> cart = new();
    List<EquipmentRow> availRows = new();

    // ----- Logs (active borrows, mark as returned) + History (everything) -----
    readonly SearchBox searchLogs = new("Search borrower, equipment or room");
    readonly DataGridView dgvLogs = Ui.NewGrid();
    readonly SearchBox searchHistory = new("Search borrower, equipment or room");
    readonly DataGridView dgvHistory = Ui.NewGrid();

    public MainForm()
    {
        Text = "ACCESS – Asset Control, Checkout, and Equipment Supervision System";
        ClientSize = new Size(1240, 860);
        MinimumSize = new Size(1020, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Content;

        // Docked controls: the LAST one added is laid out first -> Fill goes in first, header last.
        Controls.Add(host);
        Controls.Add(BuildSidebar());
        Controls.Add(BuildHeader());

        AddScreen("dashboard", "Dashboard", BuildDashboardPage(), LoadDashboard);
        AddScreen("inventory", "Inventory", BuildInventoryPage(), LoadInventory);
        if (Session.CanProcess)
        {
            AddScreen("borrow", "Borrow", BuildBorrowPage(), LoadAvailable);
            AddScreen("logs", "Borrowed\n& Return Logs", BuildLogsPage(), LoadLogs, small: true);
            AddScreen("history", "Logs History", BuildHistoryPage(), LoadHistory);
        }

        RefreshAll();
        Navigate("dashboard");
    }

    // ================= Shell =================
    Control BuildHeader()
    {
        var h = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Theme.HeaderBg };
        int x = 24;

        if (Theme.Asset("csd_seal.png") is { } seal)
        {
            h.Controls.Add(new PictureBox { Image = seal, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, Bounds = new Rectangle(x, 12, 80, 80) });
            x += 96;
        }
        if (Theme.Asset("ucc_shield.png") is { } shield)
        {
            h.Controls.Add(new PictureBox { Image = shield, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, Bounds = new Rectangle(x, 12, 72, 80) });
            x += 94;
        }

        h.Controls.Add(new Label
        {
            Text = "UNIVERSITY OF CALOOCAN CITY", Font = Theme.Display(30), ForeColor = Theme.Brown,
            AutoSize = true, BackColor = Color.Transparent, Location = new Point(x, 12)
        });
        h.Controls.Add(new Label
        {
            Text = "Computer Studies Department", Font = Theme.Display(17), ForeColor = Theme.Brown,
            AutoSize = true, BackColor = Color.Transparent, Location = new Point(x + 2, 60)
        });

        var who = new Label
        {
            Text = $"{Session.User!.FullName}  ·  {Session.User.Role.ToUpperInvariant()}",
            Font = Theme.Body(10), ForeColor = Theme.Brown, AutoSize = true, BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        h.Controls.Add(who);
        h.Resize += (_, _) => who.Location = new Point(h.Width - who.Width - 28, 40);

        h.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Brown, 1.5f);
            e.Graphics.DrawLine(pen, 0, h.Height - 1, h.Width, h.Height - 1);
        };
        return h;
    }

    Control BuildSidebar()
    {
        var side = new Panel { Dock = DockStyle.Left, Width = 236, BackColor = Theme.HeaderBg, Padding = new Padding(18, 44, 18, 26) };

        var logout = new RoundButton
        {
            Text = "Logout", Dock = DockStyle.Bottom, Height = 60, Radius = 22,
            Fill = Theme.Brown, HoverFill = Theme.BrownHover, Backdrop = Theme.HeaderBg,
            Font = Theme.Display(21, FontStyle.Italic)
        };
        logout.Click += (_, _) => { LoggedOut = true; Close(); };

        side.Controls.Add(nav);      // Fill first ...
        side.Controls.Add(logout);   // ... then Bottom

        side.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Brown, 1f);
            e.Graphics.DrawLine(pen, side.Width - 1, 0, side.Width - 1, side.Height);
        };
        return side;
    }

    void AddScreen(string key, string label, Control page, Action load, bool small = false)
    {
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        host.Controls.Add(page);

        var b = new RoundButton
        {
            Text = label, Ghost = true, Fill = Theme.Brown, Radius = 22, Backdrop = Theme.HeaderBg,
            Font = Theme.Display(small ? 15 : 21, FontStyle.Italic),
            Size = new Size(198, small ? 80 : 58), Margin = new Padding(0, 0, 0, 18)
        };
        b.Click += (_, _) => Navigate(key);
        nav.Controls.Add(b);
        screens[key] = (page, b, load);
    }

    void Navigate(string key)
    {
        foreach (var (k, s) in screens)
        {
            s.Nav.Active = k == key;
            s.Nav.Invalidate();
            s.Page.Visible = k == key;
        }
        screens[key].Page.BringToFront();
        screens[key].Load();
    }

    /// <summary>Search bar on the left, optional buttons on the right.</summary>
    static TableLayoutPanel Toolbar(SearchBox search, params Control[] right)
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1 + right.Length, RowCount = 1, AutoSize = true,
            BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 22)
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        search.Dock = DockStyle.Fill;
        search.Margin = new Padding(0, 4, 20, 4);
        t.Controls.Add(search, 0, 0);
        for (int i = 0; i < right.Length; i++)
        {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            right[i].Anchor = AnchorStyles.Left;
            t.Controls.Add(right[i], i + 1, 0);
        }
        return t;
    }

    /// <summary>Big rounded card that holds a table.</summary>
    static RoundedPanel TableCard(Control grid)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, Radius = 30, Padding = new Padding(16) };
        card.Controls.Add(grid);
        return card;
    }

    // ================= Dashboard =================
    Control BuildDashboardPage()
    {
        var page = new Page();
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 166));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 196));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // ----- banner -----
        var banner = new RoundedPanel
        {
            Dock = DockStyle.Fill, FillColor = Theme.Brown, BorderColor = Theme.Brown, Radius = 40,
            Padding = new Padding(36, 18, 28, 18), Margin = new Padding(0, 0, 0, 24)
        };
        var text = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        text.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, ForeColor = Color.White, Font = Theme.Body(13), BackColor = Color.Transparent,
            Text = Session.CanProcess
                ? "Monitor active equipment stocks, process borrowing requests, and track pending item returns for laboratories and classrooms."
                : "Monitor equipment stocks and availability for laboratories and classrooms."
        });
        text.Controls.Add(new Label
        {
            Dock = DockStyle.Top, Height = 52, Text = "Supervision Dashboard", ForeColor = Color.White,
            Font = Theme.Display(30), BackColor = Color.Transparent
        });
        banner.Controls.Add(text);

        if (Session.CanProcess)
        {
            var rightPane = new Panel { Dock = DockStyle.Right, Width = 310, BackColor = Color.Transparent };
            var btn = new RoundButton
            {
                Text = "New Borrow Request", PlusIcon = true, Font = Theme.Body(14, FontStyle.Bold),
                Radius = 30, Backdrop = Theme.Brown, Size = new Size(296, 64)
            };
            btn.Click += (_, _) => Navigate("borrow");
            rightPane.Controls.Add(btn);
            rightPane.Resize += (_, _) => btn.Location = new Point(rightPane.Width - btn.Width, (rightPane.Height - btn.Height) / 2);
            banner.Controls.Add(rightPane);
        }
        t.Controls.Add(banner, 0, 0);

        // ----- stat cards -----
        var stats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 24) };
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cardTotal.Dock = DockStyle.Fill; cardTotal.Margin = new Padding(0, 0, 14, 0);
        cardActive.Dock = DockStyle.Fill; cardActive.Margin = new Padding(14, 0, 0, 0);
        cardActive.ValueColor = Theme.Red;
        cardActive.SubColor = Theme.DueText;
        stats.Controls.Add(cardTotal, 0, 0);
        stats.Controls.Add(cardActive, 1, 0);
        t.Controls.Add(stats, 0, 1);

        // ----- active borrowed list (staff/admin only) -----
        if (Session.CanProcess)
        {
            Ui.ColorStatusRows(dgvDashActive);
            var listCard = new RoundedPanel { Dock = DockStyle.Fill, Radius = 36, Padding = new Padding(22, 10, 22, 20) };

            var head = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.Transparent };
            head.Controls.Add(new Label
            {
                Text = "Active Borrowed Items", Font = Theme.Body(18, FontStyle.Bold), ForeColor = Theme.Brown,
                AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 6)
            });
            head.Controls.Add(new Label
            {
                Text = "Items waiting to be returned", Font = Theme.Body(12), ForeColor = Theme.Brown,
                AutoSize = true, BackColor = Color.Transparent, Location = new Point(2, 42)
            });
            var link = new LinkLabel
            {
                Text = "View all Logs", Font = Theme.Body(12), AutoSize = true, BackColor = Color.Transparent,
                LinkColor = Theme.Brown, ActiveLinkColor = Theme.Tan, VisitedLinkColor = Theme.Brown,
                LinkBehavior = LinkBehavior.AlwaysUnderline
            };
            link.LinkClicked += (_, _) => Navigate("history");
            head.Controls.Add(link);
            head.Resize += (_, _) => link.Location = new Point(head.Width - link.Width - 6, 10);

            listCard.Controls.Add(dgvDashActive);   // Fill first ...
            listCard.Controls.Add(head);            // ... then Top
            t.Controls.Add(listCard, 0, 2);
        }

        page.Controls.Add(t);
        return page;
    }

    void LoadDashboard()
    {
        var s = AccessService.GetStats();
        cardTotal.Set(s.TotalItems.ToString(), $"Across {s.Categories} Categories");
        cardActive.Set(s.ActiveBorrowed.ToString(),
            s.Overdue > 0 ? $"{s.DueToday} Due today  ·  {s.Overdue} Overdue" : $"{s.DueToday} Due today");

        if (!Session.CanProcess) return;
        var rows = AccessService.GetBorrows(true, null).Take(8)
            .Select(b => new DashRow(b.Borrower, b.Equipment, b.Qty, b.Borrowed.ToString("MM-dd-yyyy"), b.Status))
            .ToList();
        Ui.Bind(dgvDashActive, rows, "Status");
        Ui.Headers(dgvDashActive, "Borrower=Borrower’s Name");
    }

    // ================= Inventory =================
    Control BuildInventoryPage()
    {
        var page = new Page();
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        chkArchived.Font = Theme.Body(11);
        chkArchived.ForeColor = Theme.Brown;
        chkArchived.BackColor = Color.Transparent;
        chkArchived.Margin = new Padding(0, 0, 18, 0);

        var extras = new List<Control>();
        if (Session.IsAdmin)
        {
            extras.Add(chkArchived);
            var add = new RoundButton
            {
                Text = "Add New Equipment", PlusIcon = true, Font = Theme.Body(14, FontStyle.Bold),
                Radius = 30, Size = new Size(300, 60)
            };
            add.Click += (_, _) => AddItem();
            extras.Add(add);

            Ui.AddAction(dgvInventory, "Edit", _ => "Edit", 84);
            Ui.AddAction(dgvInventory, "Toggle", o => ((InvRow)o).Src.Archived ? "Restore" : "Archive", 96);
            dgvInventory.CellClick += (_, e) =>
            {
                if (Ui.ActionClicked(dgvInventory, e) is not { } action
                    || dgvInventory.Rows[e.RowIndex].DataBoundItem is not InvRow r) return;
                if (action == "Edit") EditItem(r.Src);
                else if (r.Src.Archived) RestoreItem(r.Src);
                else ArchiveItem(r.Src);
            };
        }

        searchInv.SearchChanged += (_, _) => LoadInventory();
        chkArchived.CheckedChanged += (_, _) => LoadInventory();

        dgvInventory.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvInventory.Rows.Count
                || dgvInventory.Rows[e.RowIndex].DataBoundItem is not InvRow r) return;
            if (r.Src.Archived) { e.CellStyle.ForeColor = Color.Gray; return; }

            var col = dgvInventory.Columns[e.ColumnIndex].Name;
            if (col == "Qty" && r.Src.Status != "In Stock")
                e.CellStyle.ForeColor = r.Src.Status == "Out of Stock" ? Color.Firebrick : Theme.Red;
            else if (col == "Status")
                e.CellStyle.ForeColor = r.Src.Status switch
                {
                    "Out of Stock" => Color.Firebrick,
                    "Low Stock" => Color.DarkOrange,
                    _ => Theme.Brown
                };
        };

        t.Controls.Add(Ui.Title("Equipment Inventory Records"), 0, 0);
        t.Controls.Add(Toolbar(searchInv, extras.ToArray()), 0, 1);
        t.Controls.Add(TableCard(dgvInventory), 0, 2);
        page.Controls.Add(t);
        return page;
    }

    void LoadInventory()
    {
        var rows = AccessService.GetEquipment(searchInv.Text, chkArchived.Checked)
            .Select(e => new InvRow(e, e.Equipment, e.Brand, e.Category, $"{e.Available} / {e.Total}", e.Status))
            .ToList();
        Ui.Bind(dgvInventory, rows, "Src");
        Ui.Headers(dgvInventory, "Item=Item Name", "Qty=Available / Total Qty");
    }

    void AddItem()
    {
        using var dlg = new EquipmentDialog(AccessService.GetCategories(), null);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        Report(AccessService.SaveEquipment(null, dlg.ItemName, dlg.Brand, dlg.CategoryId, dlg.TotalQty));
    }

    void EditItem(EquipmentRow row)
    {
        using var dlg = new EquipmentDialog(AccessService.GetCategories(), row);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        Report(AccessService.SaveEquipment(row.Id, dlg.ItemName, dlg.Brand, dlg.CategoryId, dlg.TotalQty));
    }

    void ArchiveItem(EquipmentRow row)
    {
        if (MessageBox.Show($"Archive \"{row.Equipment}\"?", "Archive", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Report(AccessService.Archive(row.Id));
    }

    void RestoreItem(EquipmentRow row) => Report(AccessService.Restore(row.Id));

    // ================= Borrow =================
    Control BuildBorrowPage()
    {
        var page = new Page();
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        cboBorrowerRole.Items.AddRange(new object[] { "Student", "Faculty", "Staff", "Other" });
        cboBorrowerRole.SelectedIndex = 0;
        dtpReturn.Value = DateTime.Now.AddHours(2);

        cboEquipment.DisplayMember = "Label";
        cboEquipment.ValueMember = "Id";
        cboEquipment.SelectedIndexChanged += (_, _) => SyncQtyMax();

        // ----- left column: who / where / when -----
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 0, 18, 0) };
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) left.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(Ui.Labeled("Name", txtBorrower, required: true), 0, 0);
        left.Controls.Add(Ui.Labeled("Role", cboBorrowerRole), 0, 1);
        left.Controls.Add(Ui.Labeled("Room / Where to use", txtRoom, required: true), 0, 2);
        left.Controls.Add(Ui.Labeled("Expected Return Time", dtpReturn, required: true), 0, 3);

        // ----- right column: equipment, quantity, cart, purpose -----
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent, Margin = new Padding(18, 0, 0, 0) };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 48));

        right.Controls.Add(Ui.Labeled("Select Equipment/Item", cboEquipment, required: true), 0, 0);

        var qtyRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent };
        qtyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        qtyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        var qty = Ui.Labeled("Quantity", nudQty);
        qty.Margin = new Padding(0, 0, 12, 10);
        var addBtn = new RoundButton { Text = "Add to list", Size = new Size(158, 50), Backdrop = Theme.Card, Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Margin = new Padding(0, 0, 0, 10) };
        addBtn.Click += (_, _) => AddSelectedToCart();
        qtyRow.Controls.Add(qty, 0, 0);
        qtyRow.Controls.Add(addBtn, 1, 0);
        right.Controls.Add(qtyRow, 0, 1);

        right.Controls.Add(BuildCartBox(), 0, 2);
        right.Controls.Add(Ui.Labeled("Purpose/Notes", txtPurpose, fill: true), 0, 3);

        var cols = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent };
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cols.Controls.Add(left, 0, 0);
        cols.Controls.Add(right, 1, 0);

        var card = new RoundedPanel { Dock = DockStyle.Fill, Radius = 40, Padding = new Padding(32, 26, 32, 12) };
        card.Controls.Add(cols);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent, Margin = new Padding(0, 16, 0, 0)
        };
        buttons.Controls.Add(new RoundButton { Text = "Confirm Borrow", Size = new Size(200, 54), Radius = 26 }.OnClick(ConfirmBorrow));
        buttons.Controls.Add(new RoundButton
        {
            Text = "Clear", Size = new Size(130, 54), Radius = 26, Fill = Theme.Content, HoverFill = Theme.NavHover,
            TextColor = Theme.Brown, Outline = Theme.Tan, Margin = new Padding(0, 3, 12, 3)
        }.OnClick(ClearForm));

        t.Controls.Add(Ui.Title("Borrow Equipment"), 0, 0);
        t.Controls.Add(card, 0, 1);
        t.Controls.Add(buttons, 0, 2);
        page.Controls.Add(t);
        return page;
    }

    /// <summary>"Items to borrow" list shown in a white rounded box, like the other inputs.</summary>
    Control BuildCartBox()
    {
        var box = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 10) };
        box.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        box.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        box.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        box.Controls.Add(new Label
        {
            Text = "Items to borrow", AutoSize = true, Font = Theme.Body(11.5f, FontStyle.Bold),
            ForeColor = Theme.Brown, BackColor = Color.Transparent, Margin = new Padding(2, 0, 0, 4)
        }, 0, 0);

        // grid look: white, compact
        dgvCart.BackgroundColor = Theme.Field;
        dgvCart.DefaultCellStyle.BackColor = Theme.Field;
        dgvCart.DefaultCellStyle.Font = Theme.Body(10.5f);
        dgvCart.RowTemplate.Height = 40;
        dgvCart.ColumnHeadersHeight = 36;
        dgvCart.ColumnHeadersDefaultCellStyle.Font = Theme.Body(10, FontStyle.Bold);

        Ui.AddAction(dgvCart, "Remove", _ => "Remove", 92);
        Ui.Bind(dgvCart, cart, "EquipmentId", "Available");
        Ui.Headers(dgvCart, "Equipment=Item", "Quantity=Qty");
        dgvCart.ReadOnly = false;
        dgvCart.AllowUserToDeleteRows = true;       // Delete key removes a line
        foreach (DataGridViewColumn c in dgvCart.Columns) c.ReadOnly = c.Name != "Quantity";
        dgvCart.CellEndEdit += (_, _) => dgvCart.Refresh();
        dgvCart.DataError += (_, e) =>
        {
            e.ThrowException = false;
            MessageBox.Show("Quantity must be a whole number.", "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        dgvCart.CellClick += (_, e) =>
        {
            if (Ui.ActionClicked(dgvCart, e) == "Remove" && dgvCart.Rows[e.RowIndex].DataBoundItem is CartLine l)
                cart.Remove(l);
        };

        var frame = new RoundedPanel
        {
            Dock = DockStyle.Fill, FillColor = Theme.Field, BorderColor = Theme.Brown, BorderWidth = 2f,
            Radius = 14, Padding = new Padding(8), BackColor = Color.Transparent
        };
        frame.Controls.Add(dgvCart);
        box.Controls.Add(frame, 0, 1);
        return box;
    }

    void LoadAvailable()
    {
        var prev = cboEquipment.SelectedValue as int?;
        availRows = AccessService.GetEquipment(null, false).Where(r => r.Available > 0).ToList();
        cboEquipment.DataSource = availRows
            .Select(r => new ItemChoice(r.Id, $"{r.Equipment}  ({r.Available} available)")).ToList();
        if (prev is int id && availRows.Any(r => r.Id == id)) cboEquipment.SelectedValue = id;
        SyncQtyMax();
    }

    /// <summary>The quantity box can't go above the stock of the selected item.</summary>
    void SyncQtyMax()
    {
        if (cboEquipment.SelectedValue is int id && availRows.FirstOrDefault(r => r.Id == id) is { } r)
        {
            nudQty.Maximum = Math.Max(1, r.Available);
            if (nudQty.Value > nudQty.Maximum) nudQty.Value = nudQty.Maximum;
        }
    }

    void AddSelectedToCart()
    {
        if (cboEquipment.SelectedValue is not int id)
        {
            MessageBox.Show("Please select an equipment item.", "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        AddToCart(id, (int)nudQty.Value);
        nudQty.Value = 1;
    }

    void AddToCart(int equipmentId, int qty)
    {
        var r = availRows.FirstOrDefault(x => x.Id == equipmentId);
        if (r == null) return;

        var line = cart.FirstOrDefault(c => c.EquipmentId == equipmentId);
        if (line == null)
            cart.Add(new CartLine { EquipmentId = r.Id, Equipment = r.Equipment, Available = r.Available, Quantity = qty });
        else
            line.Quantity += qty;       // same item again adds more (capped at stock)
        dgvCart.Refresh();
    }

    void ClearForm()
    {
        cart.Clear();
        txtBorrower.Clear(); txtRoom.Clear(); txtPurpose.Clear();
        cboBorrowerRole.SelectedIndex = 0;
        nudQty.Value = 1;
        dtpReturn.Value = DateTime.Now.AddHours(2);
    }

    void ConfirmBorrow()
    {
        dgvCart.EndEdit();

        if (string.IsNullOrWhiteSpace(txtBorrower.Text) || string.IsNullOrWhiteSpace(txtRoom.Text))
        {
            MessageBox.Show("Please enter the borrower's name and the room.", "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Single-item requests: if nothing was added to the list yet, use the item + quantity that is selected.
        if (cart.Count == 0 && cboEquipment.SelectedValue is int sel)
            AddToCart(sel, (int)nudQty.Value);

        if (cart.Count == 0)
        {
            MessageBox.Show("Please select an equipment item first.", "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var lines = cart.Select(c => new BorrowLine(c.EquipmentId, c.Quantity)).ToList();
        var err = AccessService.Borrow(txtBorrower.Text, cboBorrowerRole.Text, txtRoom.Text, txtPurpose.Text,
                                       dtpReturn.Value, lines, Session.User!.Id);
        if (err != null)
        {
            MessageBox.Show(err, "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshAll();                // stock may have changed under us
            return;
        }

        ClearForm();
        RefreshAll();
        MessageBox.Show("Borrow request processed.", "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ================= Borrowing & Return Logs (active borrows) =================
    Control BuildLogsPage()
    {
        var page = new Page();
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        dgvLogs.RowTemplate.Height = 66;
        Ui.ColorStatusRows(dgvLogs);
        Ui.AddAction(dgvLogs, "Return", _ => "Mark as Returned", 120);
        dgvLogs.CellClick += (_, e) =>
        {
            if (Ui.ActionClicked(dgvLogs, e) == "Return" && dgvLogs.Rows[e.RowIndex].DataBoundItem is LogRow r)
                ReturnRecord(r.Id);
        };
        searchLogs.SearchChanged += (_, _) => LoadLogs();

        t.Controls.Add(Ui.Title("Borrowing & Return Logs"), 0, 0);
        t.Controls.Add(Toolbar(searchLogs), 0, 1);
        t.Controls.Add(TableCard(dgvLogs), 0, 2);
        page.Controls.Add(t);
        return page;
    }

    void LoadLogs()
    {
        var rows = AccessService.GetBorrows(true, searchLogs.Text)
            .Select(b => new LogRow(b.Id, b.Borrower, $"{b.Equipment} / {b.Qty}", b.Room,
                                    b.Borrowed.ToString("yyyy-MM-dd  hh:mm tt"), b.Status))
            .ToList();
        Ui.Bind(dgvLogs, rows, "Id");
        Ui.Headers(dgvLogs, "Borrower=Borrower’s Name", "Item=Item/Qty", "Time=TimeStamp");
    }

    void ReturnRecord(int id)
    {
        if (MessageBox.Show("Mark this record as returned?", "Return", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        Report(AccessService.Return(id));
    }

    // ================= Borrower History Log (everything) =================
    Control BuildHistoryPage()
    {
        var page = new Page();
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        dgvHistory.RowTemplate.Height = 66;
        Ui.ColorStatusRows(dgvHistory);
        searchHistory.SearchChanged += (_, _) => LoadHistory();

        t.Controls.Add(Ui.Title("Borrower History Log"), 0, 0);
        t.Controls.Add(Toolbar(searchHistory), 0, 1);
        t.Controls.Add(TableCard(dgvHistory), 0, 2);
        page.Controls.Add(t);
        return page;
    }

    void LoadHistory()
    {
        var rows = AccessService.GetBorrows(false, searchHistory.Text)
            .Select(b => new HistRow(b.Id, b.Borrower, b.Role, $"{b.Equipment} (x{b.Qty})",
                                     b.Borrowed.ToString("yyyy-MM-dd  hh:mm tt"),
                                     b.Returned?.ToString("yyyy-MM-dd  hh:mm tt") ?? "—", b.Status))
            .ToList();
        Ui.Bind(dgvHistory, rows, "Id");
        Ui.Headers(dgvHistory, "Borrower=Borrower Details", "BorrowedAt=Borrowed At", "ReturnedAt=Returned At");
    }

    // ================= Shared =================
    void Report(string? error)
    {
        if (error != null)
            MessageBox.Show(error, "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        RefreshAll();
    }

    void RefreshAll()
    {
        LoadDashboard();
        LoadInventory();
        if (Session.CanProcess)
        {
            LoadAvailable();
            LoadLogs();
            LoadHistory();
        }
    }
}

static class ButtonExtensions
{
    /// <summary>Lets us write: new RoundButton { ... }.OnClick(DoSomething)</summary>
    public static T OnClick<T>(this T b, Action action) where T : Button
    {
        b.Click += (_, _) => action();
        return b;
    }
}
