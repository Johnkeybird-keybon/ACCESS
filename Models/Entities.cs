using System.ComponentModel.DataAnnotations;

namespace AccessSystem.Models;

public class User
{
    public int Id { get; set; }
    [MaxLength(50)]  public string Username { get; set; } = "";
    [MaxLength(120)] public string FullName { get; set; } = "";
    [MaxLength(100)] public string PasswordHash { get; set; } = "";
    /// <summary>admin | staff | student</summary>
    [MaxLength(20)]  public string Role { get; set; } = "student";
    public bool IsActive { get; set; } = true;
}

public class Category
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    public List<Equipment> Equipment { get; set; } = new();
}

public class Equipment
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(80)]  public string Brand { get; set; } = "";
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int TotalQty { get; set; }
    public int AvailableQty { get; set; }
    public bool IsArchived { get; set; }
}

public class BorrowRecord
{
    public int Id { get; set; }
    [MaxLength(120)] public string BorrowerName { get; set; } = "";
    [MaxLength(30)]  public string BorrowerRole { get; set; } = "";
    public int EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public int Quantity { get; set; }
    [MaxLength(100)] public string Room { get; set; } = "";
    [MaxLength(500)] public string? Purpose { get; set; }
    public DateTime BorrowedAt { get; set; }
    public DateTime ExpectedReturn { get; set; }
    /// <summary>null = still borrowed</summary>
    public DateTime? ReturnedAt { get; set; }
    public int ProcessedById { get; set; }
    public User ProcessedBy { get; set; } = null!;
}
