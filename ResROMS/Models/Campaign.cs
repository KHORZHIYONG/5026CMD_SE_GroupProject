using System.ComponentModel.DataAnnotations;

namespace ResROMS.Models;

// Restaurant-local calendar dates. Actual order-time redemption will be
// integrated by the Orders & Payments team in a subsequent step.
public sealed class Campaign
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [StringLength(24)] public string? PromoCode { get; set; }
    [Required, StringLength(20)] public string Type { get; set; } = "Discount";
    [Required, StringLength(20)] public string DiscountType { get; set; } = "None";
    public decimal DiscountValue { get; set; }
    [Required, StringLength(1000)] public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsEnabled { get; set; } = true;
    // No fake demo redemption counts: this stays zero until Orders is integrated.
    public int UsageCount { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
