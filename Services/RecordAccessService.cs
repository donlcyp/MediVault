using MediVault.Constants;
using MediVault.Data;
using MediVault.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace MediVault.Services;

public class RecordAccessService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RecordAccessService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<bool> CanViewRecordAsync(ClaimsPrincipal principal, PatientRecord record)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user == null)
        {
            return false;
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(Roles.SystemAdministrator))
        {
            return false;
        }

        if (roles.Contains(Roles.Doctor))
        {
            return record.AssignedDoctorId == user.Id;
        }

        if (roles.Contains(Roles.Nurse))
        {
            return record.AssignedNurseId == user.Id;
        }

        return false;
    }

    public async Task<bool> CanEditRecordAsync(ClaimsPrincipal principal, PatientRecord record)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user == null)
        {
            return false;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return roles.Contains(Roles.Doctor) && record.AssignedDoctorId == user.Id;
    }

    public async Task<bool> CanAddCareNoteAsync(ClaimsPrincipal principal, PatientRecord record)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user == null)
        {
            return false;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return roles.Contains(Roles.Nurse) && record.AssignedNurseId == user.Id;
    }

    public async Task<bool> CanPrintRecordAsync(ClaimsPrincipal principal, PatientRecord record)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user == null)
        {
            return false;
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(Roles.Doctor))
        {
            return record.AssignedDoctorId == user.Id;
        }

        return roles.Contains(Roles.RecordOfficer);
    }

    public async Task<string> GetDashboardActionAsync(ClaimsPrincipal principal)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user == null)
        {
            return "Index";
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(Roles.SystemAdministrator))
        {
            return "Admin";
        }

        if (roles.Contains(Roles.Doctor))
        {
            return "Doctor";
        }

        if (roles.Contains(Roles.Nurse))
        {
            return "Nurse";
        }

        if (roles.Contains(Roles.RecordOfficer))
        {
            return "RecordOfficer";
        }

        return "Index";
    }
}
