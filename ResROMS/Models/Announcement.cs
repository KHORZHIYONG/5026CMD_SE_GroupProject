using System.ComponentModel.DataAnnotations;

namespace ResROMS.Models;

// Publication is evaluated in UTC each time the item is viewed. A future
// publication requires no scheduled job and an expired post ceases to appear.
public sealed class Announcement
{
    public int Id { get; set; }
    [Required, StringLength(140)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Body { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Audience { get; set; } = "All";
    public DateTime StartsUtc { get; set; }
    public DateTime? EndsUtc { get; set; }
    public bool IsPublished { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? FirstPublishedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
