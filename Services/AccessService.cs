using AccessSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccessSystem.Services;

// Row types shown in the grids (property names become the column headers).
public record EquipmentRow(int Id, string Equipment, string Brand, string Category, int CategoryId,
                           int Available, int Total, string Status, bool Archived);

public record BorrowRow(int Id, string Borrower, string Role, string Equipment, int Qty, string Room,
                        DateTime Borrowed, DateTime DueBack, DateTime? Returned, string Status);

public record BorrowLine(int EquipmentId, int Quantity);

public record DashboardStats(int TotalItems, int Categories, int ActiveBorrowed, int DueToday, int Overdue);

/// <summary>Same business rules as the old web API, minus the HTTP layer.</summary>
public static class AccessService
{
    static string StockStatus(int available, int total) =>
        available == 0 ? "Out of Stock" : available * 4 <= total ? "Low Stock" : "In Stock";

    static string BorrowStatus(DateTime? returned, DateTime due) =>
        returned != null ? "Returned" : due < DateTime.Now ? "Overdue" : "Active";

    // ---------- Auth ----------
    public static User? Login(string username, string password, string role)
    {
        using var db = Program.NewDb();
        var user = db.Users.AsNoTracking().FirstOrDefault(u => u.Username == username && u.IsActive);
        if (user == null
            || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)
            || !string.Equals(user.Role, role, StringComparison.OrdinalIgnoreCase))
            return null;
        return user;
    }

    // ---------- Equipment ----------
    public static List<Category> GetCategories()
    {
        using var db = Program.NewDb();
        return db.Categories.AsNoTracking().OrderBy(c => c.Name).ToList();
    }

    public static List<EquipmentRow> GetEquipment(string? search, bool includeArchived)
    {
        using var db = Program.NewDb();
        var q = db.Equipment.AsNoTracking().Include(e => e.Category).AsQueryable();
        if (!includeArchived) q = q.Where(e => !e.IsArchived);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(e => e.Name.Contains(s) || e.Brand.Contains(s) || e.Category.Name.Contains(s));
        }

        return q.OrderBy(e => e.IsArchived).ThenBy(e => e.Name).ToList()
            .Select(e => new EquipmentRow(e.Id, e.Name, e.Brand, e.Category.Name, e.CategoryId,
                e.AvailableQty, e.TotalQty, StockStatus(e.AvailableQty, e.TotalQty), e.IsArchived))
            .ToList();
    }

    /// <summary>Add (id == null) or edit. Returns an error message, or null on success.</summary>
    public static string? SaveEquipment(int? id, string name, string brand, int categoryId, int totalQty)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Name is required.";
        if (totalQty < 1) return "Total quantity must be at least 1.";

        using var db = Program.NewDb();
        if (!db.Categories.Any(c => c.Id == categoryId)) return "Selected category does not exist.";

        if (id == null)
        {
            db.Equipment.Add(new Equipment
            {
                Name = name.Trim(), Brand = brand.Trim(), CategoryId = categoryId,
                TotalQty = totalQty, AvailableQty = totalQty
            });
        }
        else
        {
            var e = db.Equipment.Find(id.Value);
            if (e == null) return "Equipment not found.";

            var borrowed = e.TotalQty - e.AvailableQty;
            if (totalQty < borrowed)
                return $"{borrowed} unit(s) are currently borrowed, so the total can't be lower than {borrowed}.";

            e.Name = name.Trim();
            e.Brand = brand.Trim();
            e.CategoryId = categoryId;
            e.TotalQty = totalQty;
            e.AvailableQty = totalQty - borrowed;
        }

        db.SaveChanges();
        return null;
    }

    public static string? Archive(int id)
    {
        using var db = Program.NewDb();
        var e = db.Equipment.Find(id);
        if (e == null) return "Equipment not found.";
        if (e.AvailableQty < e.TotalQty)
            return "Some units are still borrowed. Wait until they are returned before archiving.";
        e.IsArchived = true;
        db.SaveChanges();
        return null;
    }

    public static string? Restore(int id)
    {
        using var db = Program.NewDb();
        var e = db.Equipment.Find(id);
        if (e == null) return "Equipment not found.";
        e.IsArchived = false;
        db.SaveChanges();
        return null;
    }

    // ---------- Borrowing ----------
    public static List<BorrowRow> GetBorrows(bool activeOnly, string? search)
    {
        using var db = Program.NewDb();
        var q = db.BorrowRecords.AsNoTracking().Include(b => b.Equipment).AsQueryable();
        if (activeOnly) q = q.Where(b => b.ReturnedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(b => b.BorrowerName.Contains(s) || b.Equipment.Name.Contains(s) || b.Room.Contains(s));
        }

        return q.OrderByDescending(b => b.BorrowedAt).Take(500).ToList()
            .Select(b => new BorrowRow(b.Id, b.BorrowerName, b.BorrowerRole, b.Equipment.Name, b.Quantity,
                b.Room, b.BorrowedAt, b.ExpectedReturn, b.ReturnedAt, BorrowStatus(b.ReturnedAt, b.ExpectedReturn)))
            .ToList();
    }

    /// <summary>
    /// Borrows every line in ONE transaction: if any item lacks stock, nothing is saved.
    /// Stock is decremented with an atomic "UPDATE ... WHERE AvailableQty >= qty".
    /// </summary>
    public static string? Borrow(string borrower, string borrowerRole, string room, string? purpose,
                                 DateTime expectedReturn, IReadOnlyList<BorrowLine> lines, int processedById)
    {
        if (expectedReturn <= DateTime.Now) return "Expected return time must be in the future.";
        if (lines.Count == 0) return "The cart is empty.";

        using var db = Program.NewDb();
        using var tx = db.Database.BeginTransaction();   // disposed without Commit() = rolled back

        foreach (var line in lines)
        {
            var id = line.EquipmentId;
            var qty = line.Quantity;
            if (qty < 1) return "Quantities must be at least 1.";

            var updated = db.Equipment
                .Where(e => e.Id == id && !e.IsArchived && e.AvailableQty >= qty)
                .ExecuteUpdate(s => s.SetProperty(e => e.AvailableQty, e => e.AvailableQty - qty));

            if (updated == 0)
            {
                var eq = db.Equipment.AsNoTracking().FirstOrDefault(e => e.Id == id);
                return eq == null || eq.IsArchived
                    ? "An item was not found or is archived."
                    : $"Not enough stock for {eq.Name}. Only {eq.AvailableQty} available.";
            }

            db.BorrowRecords.Add(new BorrowRecord
            {
                BorrowerName = borrower.Trim(),
                BorrowerRole = borrowerRole.Trim(),
                EquipmentId = id,
                Quantity = qty,
                Room = room.Trim(),
                Purpose = string.IsNullOrWhiteSpace(purpose) ? null : purpose.Trim(),
                BorrowedAt = DateTime.Now,
                ExpectedReturn = expectedReturn,
                ProcessedById = processedById
            });
        }

        db.SaveChanges();
        tx.Commit();
        return null;
    }

    public static string? Return(int recordId)
    {
        using var db = Program.NewDb();
        var rec = db.BorrowRecords.AsNoTracking().FirstOrDefault(b => b.Id == recordId);
        if (rec == null) return "Record not found.";

        using var tx = db.Database.BeginTransaction();

        DateTime? now = DateTime.Now;
        // Only succeeds while still active, so a double drop can't restock twice.
        var updated = db.BorrowRecords
            .Where(b => b.Id == recordId && b.ReturnedAt == null)
            .ExecuteUpdate(s => s.SetProperty(b => b.ReturnedAt, now));
        if (updated == 0) return "This item was already returned.";

        var qty = rec.Quantity;
        var equipmentId = rec.EquipmentId;
        db.Equipment.Where(e => e.Id == equipmentId)
            .ExecuteUpdate(s => s.SetProperty(e => e.AvailableQty, e => e.AvailableQty + qty));

        tx.Commit();
        return null;
    }

    // ---------- Dashboard ----------
    public static DashboardStats GetStats()
    {
        using var db = Program.NewDb();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var now = DateTime.Now;

        var totalItems = db.Equipment.Where(e => !e.IsArchived).Sum(e => (int?)e.TotalQty) ?? 0;
        var categories = db.Equipment.Where(e => !e.IsArchived).Select(e => e.CategoryId).Distinct().Count();

        var active = db.BorrowRecords.Where(b => b.ReturnedAt == null);
        var activeBorrowed = active.Sum(b => (int?)b.Quantity) ?? 0;
        var dueToday = active.Count(b => b.ExpectedReturn >= today && b.ExpectedReturn < tomorrow);
        var overdue = active.Count(b => b.ExpectedReturn < now);

        return new DashboardStats(totalItems, categories, activeBorrowed, dueToday, overdue);
    }
}
