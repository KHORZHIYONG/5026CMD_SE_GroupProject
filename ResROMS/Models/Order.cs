using System.ComponentModel.DataAnnotations;

namespace ResROMS.Models;

// Snapshot totals and statuses are stored with the order for reliable reporting.
public sealed class Order
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public User? Customer { get; set; }
    public int? TableId { get; set; }
    public Table? Table { get; set; }
    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    [Required, StringLength(20)] public string Status { get; set; } = "New";
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; } = 0m;
    [StringLength(24)] public string? AppliedPromoCode { get; set; }
    // A customer can request a method; only staff/admin can record a verified payment.
    [StringLength(20)] public string PaymentPreference { get; set; } = "None";
    [StringLength(100)] public string? PaymentReference { get; set; } = null;
    public decimal TotalAmount { get; set; }
    public DateTime OrderedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }
}
