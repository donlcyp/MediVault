using System.Security.Cryptography;
using System.Text;
using MediVault.Models;

namespace MediVault.Services;

public class IntegrityService
{
    private readonly byte[] _key;

    public IntegrityService(IConfiguration configuration)
    {
        var keyString = configuration["Integrity:Key"]
            ?? configuration["Encryption:Key"]
            ?? "12345678901234567890123456789012";

        _key = Encoding.UTF8.GetBytes(keyString);
    }

    public string ComputePatientRecordHash(PatientRecord record)
    {
        var payload = string.Join("|",
            record.PatientId,
            record.FirstName,
            record.LastName,
            record.Age,
            record.Diagnosis,
            record.Status,
            record.DateAdmitted.ToUniversalTime().Ticks,
            record.MedicalHistoryEncrypted,
            record.DiagnosesEncrypted,
            record.PrescriptionsEncrypted,
            record.AssignedDoctorId ?? string.Empty,
            record.AssignedNurseId ?? string.Empty);

        using var hmac = new HMACSHA256(_key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    public void StampPatientRecord(PatientRecord record)
    {
        record.IntegrityHash = ComputePatientRecordHash(record);
    }

    public bool VerifyPatientRecord(PatientRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.IntegrityHash))
        {
            return false;
        }

        var expected = ComputePatientRecordHash(record);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(record.IntegrityHash));
    }
}
