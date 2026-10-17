using System.ComponentModel.DataAnnotations;

namespace ResROMS.Models;

// Manual payment RECORD, not an online card processor. One transaction per order
// in this phase. A refunded transaction remains visible for audit/history.
public sealed class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    [Required, StringLength(20)] public string Method { get; set; } = "Cash";
    [Required, StringLength(20)] public string Status { get; set; } = "Paid";
    public decimal Amount { get; set; }
    [StringLength(100)] public string? Reference { get; set; }
    public int? RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }
    public DateTime RecordedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RefundedUtc { get; set; }
}
