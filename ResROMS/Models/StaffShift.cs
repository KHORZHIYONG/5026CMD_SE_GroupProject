using System.ComponentModel.DataAnnotations;

namespace ResROMS.Models;

// A specific shift, not a recurring template. Times are restaurant-local
// (Asia/Kuala_Lumpur), consistent with OperatingHours and Reservations UI.
public sealed class StaffShift
{
    public int Id { get; set; }
    public int StaffUserId { get; set; }
    public User StaffUser { get; set; } = null!;
    public DateTime StartLocal { get; set; }
    public DateTime EndLocal { get; set; }
    [StringLength(300)] public string Notes { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
