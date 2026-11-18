using System.ComponentModel.DataAnnotations.Schema;

namespace MAQ_API.Models;

/// <summary>Single row (Id = 1) holding the running balance of the ledger.</summary>
public class CompanyFinance
{
    public const int SingletonId = 1;

    public int Id { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Balance { get; set; }

    public DateTime UpdatedAt { get; set; }
}
