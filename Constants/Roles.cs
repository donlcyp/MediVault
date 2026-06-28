namespace MediVault.Constants;

public static class Roles
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string Doctor = "Doctor";
    public const string Nurse = "Nurse";
    public const string RecordOfficer = "RecordOfficer";

    public static readonly string[] All =
    [
        SystemAdministrator,
        Doctor,
        Nurse,
        RecordOfficer
    ];

    public static readonly string[] Staff =
    [
        SystemAdministrator,
        Doctor,
        Nurse,
        RecordOfficer
    ];
}
