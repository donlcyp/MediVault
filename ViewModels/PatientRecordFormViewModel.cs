using System.ComponentModel.DataAnnotations;

namespace MediVault.ViewModels;

public class PatientRecordFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "Patient Account")]
    public string PatientId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Range(0, 150)]
    public int Age { get; set; }

    public string Diagnosis { get; set; } = string.Empty;

    public string Status { get; set; } = "Stable";

    [Display(Name = "Date Admitted")]
    [DataType(DataType.Date)]
    public DateTime DateAdmitted { get; set; } = DateTime.UtcNow.Date;

    [Display(Name = "Medical History")]
    public string MedicalHistory { get; set; } = string.Empty;

    public string Diagnoses { get; set; } = string.Empty;

    public string Prescriptions { get; set; } = string.Empty;

    public string? ReturnAction { get; set; }
}
