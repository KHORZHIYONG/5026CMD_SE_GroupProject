using System.ComponentModel.DataAnnotations;

namespace ResROMS.Models
{
    public class User
    {
        public int Id { get; set; }
        [Required] public string FullName { get; set; } = string.Empty;
        [Required] public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = "Staff";
        public bool IsActive { get; set; } = true;
        public DateTime DateJoined { get; set; } = DateTime.UtcNow;
    }

    public class Category
    {
        public int Id { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        public List<MenuItem> MenuItems { get; set; } = new();
    }

    public class MenuItem
    {
        public int Id { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string ImageUrl { get; set; } = string.Empty;

        public string? DietaryTag { get; set; }

        // Foreign Key
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }

    public class DiningArea
    {
        public int Id { get; set; }
        [Required, StringLength(80)] public string Name { get; set; } = string.Empty;
        public List<Table> Tables { get; set; } = new();
    }

    public class Table
    {
        public int Id { get; set; }
        [Required, StringLength(20)] public string TableNumber { get; set; } = string.Empty;
        public int Capacity { get; set; }
        [StringLength(20)] public string Status { get; set; } = "Available";
        [StringLength(20)] public string Shape { get; set; } = "Square";

        // Foreign Key
        public int DiningAreaId { get; set; }
        public DiningArea? DiningArea { get; set; }
    }
}