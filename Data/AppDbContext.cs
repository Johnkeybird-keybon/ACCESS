using AccessSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccessSystem.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<BorrowRecord> BorrowRecords => Set<BorrowRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();
        b.Entity<Category>().HasIndex(c => c.Name).IsUnique();

        b.Entity<Equipment>(e =>
        {
            // Safety net: stock can never go negative or exceed the total.
            e.ToTable(t => t.HasCheckConstraint("CK_Equipment_Qty",
                "AvailableQty >= 0 AND AvailableQty <= TotalQty"));
            e.HasOne(x => x.Category).WithMany(c => c.Equipment)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<BorrowRecord>(r =>
        {
            r.HasIndex(x => x.ReturnedAt);
            r.HasOne(x => x.Equipment).WithMany()
                .HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne(x => x.ProcessedBy).WithMany()
                .HasForeignKey(x => x.ProcessedById).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
