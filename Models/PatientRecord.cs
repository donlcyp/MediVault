using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediVault.Models;

public class PatientRecord
{
    [Key]
    public int Id { get; set; }

    [MaxLength(40)]
    public string PatientId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    public string PatientName => $"{FirstName} {LastName}";

    public int Age { get; set; }

    [Required]
    [MaxLength(160)]
    public string Diagnosis { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = string.Empty;

    public DateTime DateAdmitted { get; set; } = DateTime.UtcNow;

    // This field can be encrypted in transit/at rest as per requirements
    [Required]
    public string MedicalHistoryEncrypted { get; set; } = string.Empty;

    public string DiagnosesEncrypted { get; set; } = string.Empty;

    public string PrescriptionsEncrypted { get; set; } = string.Empty;

    public string? AssignedDoctorId { get; set; }

    public string? AssignedNurseId { get; set; }

    public string IntegrityHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public ICollection<BillingRecord> BillingRecords { get; set; } = new List<BillingRecord>();
}
