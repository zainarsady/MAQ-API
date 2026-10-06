using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MAQ_API.Models;

public class TempahanRamadan
{
    [Key]
    public int TempahanRamadanId { get; set; }

    public DateOnly BookedDate { get; set; }

    [Required, MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string ContactNumber { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string ICNumber { get; set; } = string.Empty;

    [Range(1, 120)]
    public int Age { get; set; }

    [Required, MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    [Range(0, 99999999.99)]
    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }
}
