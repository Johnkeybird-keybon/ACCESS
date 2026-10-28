using AccessSystem.Models;
using AccessSystem.Services;

namespace AccessSystem.Forms;

/// <summary>Add / edit one equipment item (admin only).</summary>
public class EquipmentDialog : Form
{
    readonly TextBox txtName = new() { MaxLength = 120 };
    readonly TextBox txtBrand = new() { MaxLength = 80 };
    readonly ComboBox cboCategory = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly NumericUpDown nudTotal = new() { Minimum = 1, Maximum = 100000, Value = 1 };

    public string ItemName => txtName.Text.Trim();
    public string Brand => txtBrand.Text.Trim();
    public int CategoryId => (int)cboCategory.SelectedValue!;
    public int TotalQty => (int)nudTotal.Value;

    public EquipmentDialog(List<Category> categories, EquipmentRow? existing)
    {
        Text = existing == null ? "Add equipment" : "Edit equipment";
        ClientSize = new Size(470, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Content;

        cboCategory.DataSource = categories;
        cboCategory.DisplayMember = "Name";
        cboCategory.ValueMember = "Id";

        if (existing != null)
        {
            txtName.Text = existing.Equipment;
            txtBrand.Text = existing.Brand;
            cboCategory.SelectedValue = existing.CategoryId;
            nudTotal.Value = Math.Clamp(existing.Total, 1, 100000);
        }

        var heading = new Label
        {
            Text = existing == null ? "Add New Equipment" : "Edit Equipment",
            Font = Theme.Display(28), ForeColor = Theme.Brown, AutoSize = true,
            Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 10)
        };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        stack.Controls.Add(Ui.Labeled("Name", txtName, required: true), 0, 0);
        stack.Controls.Add(Ui.Labeled("Brand", txtBrand), 0, 1);
        stack.Controls.Add(Ui.Labeled("Category", cboCategory, required: true), 0, 2);
        stack.Controls.Add(Ui.Labeled("Total quantity", nudTotal, required: true), 0, 3);

        var btnOk = new RoundButton { Text = "Save", Size = new Size(130, 46), Margin = new Padding(8, 0, 0, 0) };
        btnOk.Click += (_, _) => OnSave();
        var btnCancel = new RoundButton
        {
            Text = "Cancel", Size = new Size(130, 46), DialogResult = DialogResult.Cancel,
            Fill = Theme.Content, HoverFill = Theme.NavHover, TextColor = Theme.Brown, Outline = Theme.Tan
        };
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 66, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0), BackColor = Color.Transparent
        };
        bar.Controls.Add(btnOk);
        bar.Controls.Add(btnCancel);

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(36, 28, 36, 16), BackColor = Theme.Content };
        // Docked controls: the last one added is laid out first (Fill goes in first).
        body.Controls.Add(stack);
        body.Controls.Add(bar);
        body.Controls.Add(heading);
        Controls.Add(body);
    }

    void OnSave()
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("Please enter a name.", "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (cboCategory.SelectedValue == null)
        {
            MessageBox.Show("Please choose a category.", "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
