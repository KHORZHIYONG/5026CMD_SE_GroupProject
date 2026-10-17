using System.ComponentModel.DataAnnotations;

namespace ResROMS.Models;

// One restaurant per deployment. This is deliberately separate from the customer User model.
public sealed class RestaurantProfile
{
    public int Id { get; set; } = 1;
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [StringLength(40)] public string Phone { get; set; } = string.Empty;
    [StringLength(254), EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Address { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
