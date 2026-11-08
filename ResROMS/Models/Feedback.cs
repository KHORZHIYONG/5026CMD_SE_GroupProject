using System.ComponentModel.DataAnnotations;

namespace ResROMS.Models;

// A customer's rating of ONE completed order OR ONE completed reservation (never both).
public sealed class Feedback
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public User Customer { get; set; } = null!;
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    [Range(1, 5)] public int Rating { get; set; }
    [StringLength(500)] public string Comment { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
