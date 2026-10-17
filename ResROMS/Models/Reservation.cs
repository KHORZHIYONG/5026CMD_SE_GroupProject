using System.ComponentModel.DataAnnotations;

namespace ResROMS.Models;

// The booking interval is stored as UTC to avoid server/local-time ambiguity.
// ReservationClock converts the restaurant's Malaysia-local input into UTC.
public sealed class Reservation
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public User Customer { get; set; } = null!;
    public int TableId { get; set; }
    public Table Table { get; set; } = null!;
    public int PartySize { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    [Required, StringLength(20)]
    public string Status { get; set; } = "Pending";
    [StringLength(500)]
    public string SpecialRequests { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledUtc { get; set; }
}
