using System.ComponentModel.DataAnnotations;

namespace MediVault.Models;

public class Appointment
{
    [Key]
    public int Id { get; set; }

    public int PatientRecordId { get; set; }

    public PatientRecord? PatientRecord { get; set; }

    [Required]
    public DateTime ScheduledAt { get; set; }

    [Required]
    [StringLength(120)]
    public string Purpose { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string Status { get; set; } = "Scheduled";

    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
