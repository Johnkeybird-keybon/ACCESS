using AccessSystem.Models;

namespace AccessSystem.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (!db.Users.Any())
        {
            // Default accounts - CHANGE THESE PASSWORDS before real use.
            db.Users.AddRange(
                new User { Username = "admin",   FullName = "System Administrator", Role = "admin",   PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123") },
                new User { Username = "staff",   FullName = "Laboratory Staff",     Role = "staff",   PasswordHash = BCrypt.Net.BCrypt.HashPassword("staff123") },
                new User { Username = "student", FullName = "Student Viewer",       Role = "student", PasswordHash = BCrypt.Net.BCrypt.HashPassword("student123") });
            db.SaveChanges();
        }

        if (!db.Categories.Any())
        {
            db.Categories.AddRange(new[]
            {
                "AV Equipment", "Cables & Adapters", "Peripherals", "Laptops & Tablets",
                "Networking", "Lab Tools", "Others"
            }.Select(n => new Category { Name = n }));
            db.SaveChanges();
        }

        if (!db.Equipment.Any())
        {
            int Cat(string n) => db.Categories.First(c => c.Name == n).Id;
            Equipment E(string name, string brand, string cat, int avail, int total) => new()
            { Name = name, Brand = brand, CategoryId = Cat(cat), AvailableQty = avail, TotalQty = total };

            db.Equipment.AddRange(
                E("Epson EB-X06 LCD Projector",          "Epson",    "AV Equipment",      4, 8),
                E("HDMI Cable (10m Heavy Duty)",         "Epson",    "Cables & Adapters", 3, 16),
                E("Logitech Wireless Presenter Pointer", "Logitech", "Peripherals",       2, 10),
                E("Dell Latitude 3420 Laptop",           "Dell",     "Laptops & Tablets", 3, 16),
                E("Yamaha Portable Speaker Unit",        "Yamaha",   "AV Equipment",      1, 2),
                E("Cisco 8-Port Gigabit Switch",         "Cisco",    "Networking",        6, 6));
            db.SaveChanges();
        }
    }
}
