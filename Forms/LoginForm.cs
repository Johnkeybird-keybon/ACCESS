using System.Drawing.Drawing2D;
using AccessSystem.Services;

namespace AccessSystem.Forms;

/// <summary>Login screen styled after the Figma "Desktop" frame: gradient window, big rounded card.</summary>
public class LoginForm : Form
{
    readonly TextBox txtUser = new() { MaxLength = 50 };
    readonly TextBox txtPass = new() { UseSystemPasswordChar = true };
    readonly ComboBox cboRole = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    static readonly Color Dark = Color.FromArgb(0x2E, 0x24, 0x1E);
    static readonly Font fUniversity = Theme.Body(20, FontStyle.Bold);
    static readonly Font fDept = Theme.Display(36);
    static readonly Font fAccess = Theme.Display(26, FontStyle.Italic);
    static readonly Font fTagline = Theme.Display(15, FontStyle.Italic);
    readonly Image? shield = Theme.Asset("ucc_shield.png");
    readonly Image? seal = Theme.Asset("csd_seal.png");

    public LoginForm()
    {
        Text = "ACCESS – Login";
        ClientSize = new Size(1100, 690);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(0xC8, 0xC8, 0xB3);

        cboRole.Items.AddRange(new object[] { "Administrator", "Staff", "Student" });
        cboRole.SelectedIndex = 0;

        // ----- left half of the card: the form -----
        const int fx = 100, fw = 410;
        var title = new Label
        {
            Text = "LOGIN", Font = Theme.Display(40), ForeColor = Theme.Brown, BackColor = Color.Transparent,
            AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(fx, 78, fw, 66)
        };

        var user = Ui.Labeled("USERNAME", txtUser, labelColor: Dark);
        user.Bounds = new Rectangle(fx, 170, fw, 82);
        var pass = Ui.Labeled("PASSWORD", txtPass, labelColor: Dark);
        pass.Bounds = new Rectangle(fx, 268, fw, 82);
        var role = Ui.Labeled("ROLE", cboRole, labelColor: Dark);
        role.Bounds = new Rectangle(fx, 366, fw, 82);
        foreach (var c in new[] { user, pass, role })
        {
            c.Dock = DockStyle.None;      // positioned by Bounds above
            foreach (Control l in c.Controls)
                if (l is Label lb) lb.Font = Theme.Display(15);
        }

        var btnLogin = new RoundButton
        {
            Text = "LOGIN", Font = Theme.Display(20), Radius = 28,
            Fill = Theme.Brown, HoverFill = Theme.BrownHover, Outline = Color.Black,
            Backdrop = Color.FromArgb(0x6B, 0x59, 0x4C),
            Bounds = new Rectangle(fx + 20, 520, fw - 40, 58)
        };
        btnLogin.Click += (_, _) => DoLogin();

        AcceptButton = btnLogin;
        Controls.AddRange(new Control[] { title, user, pass, role, btnLogin });
        txtUser.Select();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // window background: sage -> dark brown
        using (var bg = new LinearGradientBrush(ClientRectangle, Color.Empty, Color.Empty, 90f))
        {
            bg.InterpolationColors = new ColorBlend
            {
                Positions = new[] { 0f, 0.5f, 1f },
                Colors = new[]
                {
                    Color.FromArgb(0xC8, 0xC8, 0xB3), Color.FromArgb(0x98, 0x93, 0x85), Color.FromArgb(0x42, 0x39, 0x33)
                }
            };
            g.FillRectangle(bg, ClientRectangle);
        }

        // the card
        var card = new RectangleF(48, 44, ClientSize.Width - 96, ClientSize.Height - 88);
        using var path = Theme.RoundRect(card, 64);
        using (var cb = new LinearGradientBrush(card, Color.Empty, Color.Empty, 90f))
        {
            cb.InterpolationColors = new ColorBlend
            {
                Positions = new[] { 0f, 1f },
                Colors = new[] { Color.FromArgb(0xE8, 0xE6, 0xD7), Color.FromArgb(0x49, 0x3A, 0x31) }
            };
            g.FillPath(cb, path);
        }
        using (var pen = new Pen(Theme.Brown, 1.5f)) g.DrawPath(pen, path);

        // ----- right half: university branding -----
        const int rx = 600;
        if (shield != null) g.DrawImage(shield, new Rectangle(rx, 120, 76, 86));
        if (seal != null) g.DrawImage(seal, new Rectangle(rx + 92, 124, 80, 80));

        TextRenderer.DrawText(g, "University of Caloocan City", fUniversity, new Point(rx, 228), Dark, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "COMPUTER STUDIES", fDept, new Point(rx - 2, 272), Dark, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "DEPARTMENT", fDept, new Point(rx - 2, 322), Dark, TextFormatFlags.NoPadding);
        using (var line = new Pen(Dark, 3f)) g.DrawLine(line, rx, 386, rx + 400, 386);
        TextRenderer.DrawText(g, "ACCESS", fAccess, new Point(rx, 400), Dark, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "Asset Control, Checkout, and\nEquipment Supervision System", fTagline,
            new Point(rx, 436), Dark, TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { shield?.Dispose(); seal?.Dispose(); }
        base.Dispose(disposing);
    }

    void DoLogin()
    {
        var role = cboRole.SelectedItem?.ToString() switch
        {
            "Administrator" => "admin",
            "Staff" => "staff",
            "Student" => "student",
            _ => ""
        };

        try
        {
            var user = AccessService.Login(txtUser.Text.Trim(), txtPass.Text, role);
            if (user == null)
            {
                MessageBox.Show("Invalid username, password, or role.", "ACCESS",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPass.Clear();
                txtPass.Focus();
                return;
            }
            Session.User = user;
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not reach the database. Is MySQL running in XAMPP?\n\n" + ex.Message,
                "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
