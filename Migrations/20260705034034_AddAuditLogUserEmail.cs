using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediVault.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogUserEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('AuditLogs', 'UserEmail') IS NULL
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [UserEmail] nvarchar(max) NOT NULL CONSTRAINT [DF_AuditLogs_UserEmail] DEFAULT(N'');
                END

                UPDATE al
                    SET al.UserEmail = COALESCE(au.Email, al.UserId)
                FROM [AuditLogs] al
                LEFT JOIN [AspNetUsers] au ON au.Id = al.UserId
                WHERE al.UserEmail = N'';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('AuditLogs', 'UserEmail') IS NOT NULL
                BEGIN
                    ALTER TABLE [AuditLogs] DROP COLUMN [UserEmail];
                END
                """);
        }
    }
}
