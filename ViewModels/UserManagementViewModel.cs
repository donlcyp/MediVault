namespace MediVault.ViewModels;

public class UserManagementViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = [];
    public bool EmailConfirmed { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class RoleAssignmentViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class UserStatusToggleViewModel
{
    public string UserId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
