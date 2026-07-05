using MediVault.Constants;
using MediVault.Data;
using MediVault.Models;
using MediVault.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediVault.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<MediVaultContext>();
        var encryption = serviceProvider.GetRequiredService<EncryptionService>();
        var integrity = serviceProvider.GetRequiredService<IntegrityService>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var admin = await EnsureUserAsync(userManager, "admin@medivault.com", "Admin@12345", Roles.SystemAdministrator);
        var doctor = await EnsureUserAsync(userManager, "doctor@medivault.com", "Doctor@12345", Roles.Doctor);
        var nurse = await EnsureUserAsync(userManager, "nurse@medivault.com", "Nurse@12345", Roles.Nurse);
        var records = await EnsureUserAsync(userManager, "records@medivault.com", "Records@12345", Roles.RecordOfficer);

        await EnsureSampleAuditLogsAsync(context, admin, doctor, nurse);

        if (!await context.PatientRecords.AnyAsync())
        {
            context.PatientRecords.AddRange(
                CreateRecord("CHART-001", encryption, "Jane", "Doe", 34, "Hypertension", "Stable",
                    "History of elevated blood pressure. Family history of cardiovascular disease.",
                    "Primary hypertension, well controlled.",
                    "Lisinopril 10mg daily",
                    doctor?.Id,
                    nurse?.Id,
                    integrity),
                CreateRecord("CHART-004", encryption, "Jane", "Doe", 34, "Annual Physical", "Improving",
                    "Routine annual checkup.",
                    "General wellness exam.",
                    "Multivitamin daily",
                    doctor?.Id,
                    nurse?.Id,
                    integrity),
                CreateRecord("CHART-002", encryption, "John", "Smith", 58, "Type 2 Diabetes", "Serious",
                    "Diagnosed 5 years ago. Diet-controlled with medication support.",
                    "Type 2 diabetes mellitus with mild neuropathy.",
                    "Metformin 500mg twice daily",
                    doctor?.Id,
                    nurse?.Id,
                    integrity),
                CreateRecord("CHART-003", encryption, "Maria", "Garcia", 72, "Pneumonia", "Critical",
                    "Admitted with respiratory distress. Oxygen therapy initiated.",
                    "Community-acquired pneumonia.",
                    "Azithromycin 500mg daily, supplemental oxygen",
                    doctor?.Id,
                    nurse?.Id,
                    integrity));

            await context.SaveChangesAsync();
        }
        else
        {
            var existingRecords = await context.PatientRecords.ToListAsync();
            var changed = false;

            foreach (var record in existingRecords)
            {
                if (string.IsNullOrWhiteSpace(record.AssignedDoctorId))
                {
                    record.AssignedDoctorId = doctor?.Id;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(record.AssignedNurseId))
                {
                    record.AssignedNurseId = nurse?.Id;
                    changed = true;
                }

                var hash = integrity.ComputePatientRecordHash(record);
                if (record.IntegrityHash != hash)
                {
                    record.IntegrityHash = hash;
                    changed = true;
                }
            }

            if (changed)
            {
                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user != null)
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }

            return user;
        }

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
            return user;
        }

        return null;
    }

    private static PatientRecord CreateRecord(
        string patientUserId,
        EncryptionService encryption,
        string firstName,
        string lastName,
        int age,
        string diagnosis,
        string status,
        string history,
        string diagnoses,
        string prescriptions,
        string? assignedDoctorId,
        string? assignedNurseId,
        IntegrityService integrity)
    {
        var record = new PatientRecord
        {
            PatientId = patientUserId,
            FirstName = firstName,
            LastName = lastName,
            Age = age,
            Diagnosis = diagnosis,
            Status = status,
            DateAdmitted = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30)),
            MedicalHistoryEncrypted = encryption.Encrypt(history),
            DiagnosesEncrypted = encryption.Encrypt(diagnoses),
            PrescriptionsEncrypted = encryption.Encrypt(prescriptions),
            AssignedDoctorId = assignedDoctorId,
            AssignedNurseId = assignedNurseId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        integrity.StampPatientRecord(record);
        return record;
    }

    public static async Task EnsureSampleAuditLogsAsync(MediVaultContext context)
    {
        await EnsureSampleAuditLogsAsync(context, null, null, null);
    }

    private static async Task EnsureSampleAuditLogsAsync(
        MediVaultContext context,
        ApplicationUser? admin,
        ApplicationUser? doctor,
        ApplicationUser? nurse)
    {
        if (await context.AuditLogs.AnyAsync())
        {
            return;
        }

        context.AuditLogs.AddRange(
            new AuditLog
            {
                UserId = admin?.Id ?? "admin@medivault.com",
                UserEmail = admin?.Email ?? admin?.UserName ?? "admin@medivault.com",
                Action = "Login",
                Description = "Administrator signed in",
                EntityType = "User",
                Details = "System startup seed",
                IpAddress = "127.0.0.1",
                Timestamp = DateTime.UtcNow.AddHours(-2)
            },
            new AuditLog
            {
                UserId = admin?.Id ?? "admin@medivault.com",
                UserEmail = admin?.Email ?? admin?.UserName ?? "admin@medivault.com",
                Action = "Download",
                Description = "Exported audit logs CSV",
                EntityType = "AuditLog",
                Details = "System startup seed",
                IpAddress = "127.0.0.1",
                Timestamp = DateTime.UtcNow.AddHours(-1).AddMinutes(-40)
            },
            new AuditLog
            {
                UserId = doctor?.Id ?? "doctor@medivault.com",
                UserEmail = doctor?.Email ?? doctor?.UserName ?? "doctor@medivault.com",
                Action = "ViewRecord",
                Description = "Viewed patient record #1",
                EntityType = "PatientRecord",
                Details = "Jane Doe (Hypertension)",
                IpAddress = "127.0.0.1",
                Timestamp = DateTime.UtcNow.AddHours(-1)
            },
            new AuditLog
            {
                UserId = nurse?.Id ?? "nurse@medivault.com",
                UserEmail = nurse?.Email ?? nurse?.UserName ?? "nurse@medivault.com",
                Action = "UpdateRecord",
                Description = "Added care note to patient record #4",
                EntityType = "PatientRecord",
                Details = "Vitals stable, oxygen saturation improving",
                IpAddress = "127.0.0.1",
                Timestamp = DateTime.UtcNow.AddMinutes(-30)
            },
            new AuditLog
            {
                UserId = doctor?.Id ?? "doctor@medivault.com",
                UserEmail = doctor?.Email ?? doctor?.UserName ?? "doctor@medivault.com",
                Action = "ScheduleAppointment",
                Description = "Scheduled follow-up appointment",
                EntityType = "Appointment",
                Details = "Follow-up in 2 weeks",
                IpAddress = "127.0.0.1",
                Timestamp = DateTime.UtcNow.AddMinutes(-20)
            },
            new AuditLog
            {
                UserId = admin?.Id ?? "admin@medivault.com",
                UserEmail = admin?.Email ?? admin?.UserName ?? "admin@medivault.com",
                Action = "GenerateBill",
                Description = "Generated billing record",
                EntityType = "BillingRecord",
                Details = "Invoice #BILL-001",
                IpAddress = "127.0.0.1",
                Timestamp = DateTime.UtcNow.AddMinutes(-10)
            },
            new AuditLog
            {
                UserId = nurse?.Id ?? "nurse@medivault.com",
                UserEmail = nurse?.Email ?? nurse?.UserName ?? "nurse@medivault.com",
                Action = "Logout",
                Description = "Nurse signed out",
                EntityType = "User",
                Details = "Shift complete",
                IpAddress = "127.0.0.1",
                Timestamp = DateTime.UtcNow.AddMinutes(-5)
            });

        await context.SaveChangesAsync();
    }
}
