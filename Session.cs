using AccessSystem.Models;

namespace AccessSystem;

/// <summary>Who is logged in right now.</summary>
public static class Session
{
    public static User? User { get; set; }

    public static bool IsAdmin =>
        string.Equals(User?.Role, "admin", StringComparison.OrdinalIgnoreCase);

    /// <summary>Admin or staff: may process borrows/returns and see borrower names.</summary>
    public static bool CanProcess =>
        IsAdmin || string.Equals(User?.Role, "staff", StringComparison.OrdinalIgnoreCase);
}
