using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediVault.Migrations
{
    /// <inheritdoc />
    public partial class SyncCurrentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_PatientRecords_PatientId'
                      AND object_id = OBJECT_ID(N'[PatientRecords]')
                )
                BEGIN
                    DROP INDEX [IX_PatientRecords_PatientId] ON [PatientRecords];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_PatientRecords_PatientId'
                      AND object_id = OBJECT_ID(N'[PatientRecords]')
                )
                BEGIN
                    CREATE INDEX [IX_PatientRecords_PatientId] ON [PatientRecords] ([PatientId]);
                END
                """);
        }
    }
}
