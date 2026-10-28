using AccessSystem.Data;
using AccessSystem.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AccessSystem;

static class Program
{
    static DbContextOptions<AppDbContext> options = null!;

    /// <summary>Creates a short-lived DbContext. Use it in a "using" block.</summary>
    public static AppDbContext NewDb() => new(options);

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Error);

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Default in appsettings.json");
        var serverVersion = ServerVersion.Parse(config["Database:ServerVersion"] ?? "10.4.24-mariadb");

        options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, serverVersion)
            .Options;

        try
        {
            using var db = NewDb();
            db.Database.EnsureCreated();   // creates access_db + tables on first run
            DbSeeder.Seed(db);             // starter users, categories, equipment
        }
        catch (Exception ex)
        {
            MessageBox.Show("Can't reach the database. Is MySQL started in the XAMPP Control Panel?\n\n" + ex.Message,
                "ACCESS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Login -> main window -> (logout) -> login again
        while (true)
        {
            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK) break;

            using var main = new MainForm();
            main.ShowDialog();
            Session.User = null;
            if (!main.LoggedOut) break;
        }
    }
}
