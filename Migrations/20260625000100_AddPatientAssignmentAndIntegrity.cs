using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediVault.Migrations
{
    [Migration("20260625000100_AddPatientAssignmentAndIntegrity")]
    public partial class AddPatientAssignmentAndIntegrity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedDoctorId",
                table: "PatientRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedNurseId",
                table: "PatientRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityHash",
                table: "PatientRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedDoctorId",
                table: "PatientRecords");

            migrationBuilder.DropColumn(
                name: "AssignedNurseId",
                table: "PatientRecords");

            migrationBuilder.DropColumn(
                name: "IntegrityHash",
                table: "PatientRecords");
        }
    }
}
