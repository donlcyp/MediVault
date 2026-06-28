using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediVault.Models;

public class BillingRecord
{
    [Key]
    public int Id { get; set; }

    public int PatientRecordId { get; set; }

    public PatientRecord? PatientRecord { get; set; }

    [Required]
    [StringLength(160)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(40)]
    public string Status { get; set; } = "Pending";

    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
