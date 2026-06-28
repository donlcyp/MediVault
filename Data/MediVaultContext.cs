using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MediVault.Models;

public class MediVaultContext(DbContextOptions<MediVaultContext> options) : IdentityDbContext<MediVault.Data.ApplicationUser>(options)
{
    public DbSet<PatientRecord> PatientRecords { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<BillingRecord> BillingRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PatientRecord>()
            .HasMany(x => x.Appointments)
            .WithOne(x => x.PatientRecord)
            .HasForeignKey(x => x.PatientRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PatientRecord>()
            .HasMany(x => x.BillingRecords)
            .WithOne(x => x.PatientRecord)
            .HasForeignKey(x => x.PatientRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
