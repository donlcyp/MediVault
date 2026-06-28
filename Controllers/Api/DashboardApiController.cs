using MediVault.Constants;
using MediVault.Data;
using MediVault.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediVault.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardApiController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly MediVaultContext _context;
    private readonly RecordAccessService _recordAccess;
    private readonly AuditQueryService _auditQuery;

    public DashboardApiController(
        UserManager<ApplicationUser> userManager,
        MediVaultContext context,
        RecordAccessService recordAccess,
        AuditQueryService auditQuery)
    {
        _userManager = userManager;
        _context = context;
        _recordAccess = recordAccess;
        _auditQuery = auditQuery;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var dashboard = await _recordAccess.GetDashboardActionAsync(User);

        object payload;
        if (roles.Contains(Roles.SystemAdministrator))
        {
            payload = new
            {
                user = new { user.Id, Email = user.Email, Roles = roles },
                dashboard,
                counts = new
                {
                    users = await _userManager.Users.CountAsync(),
                    patientRecords = await _context.PatientRecords.CountAsync(),
                    auditLogs = await _context.AuditLogs.CountAsync()
                },
                recentAuditLogs = await _context.AuditLogs
                    .OrderByDescending(x => x.Timestamp)
                    .Take(10)
                    .Select(x => new { x.Id, x.Timestamp, x.UserId, x.Action, x.EntityType, x.Description })
                    .ToListAsync()
            };
        }
        else if (roles.Contains(Roles.Doctor))
        {
            payload = new
            {
                user = new { user.Id, Email = user.Email, Roles = roles },
                dashboard,
                counts = new
                {
                    assignedRecords = await _context.PatientRecords.CountAsync(x => x.AssignedDoctorId == user.Id && x.Status != "Discharged")
                },
                recentRecords = await _context.PatientRecords
                    .Where(x => x.AssignedDoctorId == user.Id && x.Status != "Discharged")
                    .OrderByDescending(x => x.UpdatedAt)
                    .Take(5)
                    .Select(x => new { x.Id, x.PatientId, x.PatientName, x.Age, x.Diagnosis, x.Status, x.DateAdmitted })
                    .ToListAsync()
            };
        }
        else if (roles.Contains(Roles.Nurse))
        {
            payload = new
            {
                user = new { user.Id, Email = user.Email, Roles = roles },
                dashboard,
                counts = new
                {
                    assignedRecords = await _context.PatientRecords.CountAsync(x => x.AssignedNurseId == user.Id && x.Status != "Discharged")
                },
                recentRecords = await _context.PatientRecords
                    .Where(x => x.AssignedNurseId == user.Id && x.Status != "Discharged")
                    .OrderByDescending(x => x.UpdatedAt)
                    .Take(5)
                    .Select(x => new { x.Id, x.PatientId, x.PatientName, x.Age, x.Diagnosis, x.Status, x.DateAdmitted })
                    .ToListAsync()
            };
        }
        else if (roles.Contains(Roles.RecordOfficer))
        {
            payload = new
            {
                user = new { user.Id, Email = user.Email, Roles = roles },
                dashboard,
                counts = new
                {
                    patientRecords = await _context.PatientRecords.CountAsync(),
                    appointments = await _context.Appointments.CountAsync(x => x.Status == "Scheduled"),
                    pendingBills = await _context.BillingRecords.CountAsync(x => x.Status == "Pending")
                },
                patientRecords = await _context.PatientRecords
                    .OrderByDescending(x => x.DateAdmitted)
                    .Take(10)
                    .Select(x => new { x.Id, x.PatientId, x.PatientName, x.Age, x.Diagnosis, x.Status, x.DateAdmitted })
                    .ToListAsync()
            };
        }
        else
        {
            payload = new
            {
                user = new { user.Id, Email = user.Email, Roles = roles },
                dashboard
            };
        }

        return Ok(payload);
    }

    [HttpGet("records")]
    public async Task<IActionResult> Records()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(Roles.SystemAdministrator))
        {
            return Ok(new { records = Array.Empty<object>() });
        }

        if (roles.Contains(Roles.Doctor))
        {
            var records = await _context.PatientRecords
                .Where(x => x.AssignedDoctorId == user.Id && x.Status != "Discharged")
                .OrderByDescending(x => x.DateAdmitted)
                .Select(x => new { x.Id, x.PatientId, x.PatientName, x.Age, x.Diagnosis, x.Status, x.DateAdmitted, x.UpdatedAt })
                .ToListAsync();
            return Ok(new { records });
        }

        if (roles.Contains(Roles.Nurse))
        {
            var records = await _context.PatientRecords
                .Where(x => x.AssignedNurseId == user.Id && x.Status != "Discharged")
                .OrderByDescending(x => x.DateAdmitted)
                .Select(x => new { x.Id, x.PatientId, x.PatientName, x.Age, x.Diagnosis, x.Status, x.DateAdmitted, x.UpdatedAt })
                .ToListAsync();
            return Ok(new { records });
        }

        if (roles.Contains(Roles.RecordOfficer))
        {
            var records = await _context.PatientRecords
                .OrderByDescending(x => x.DateAdmitted)
                .Select(x => new { x.Id, x.PatientId, x.PatientName, x.Age, x.Diagnosis, x.Status, x.DateAdmitted, x.UpdatedAt })
                .ToListAsync();
            return Ok(new { records });
        }

        return Forbid();
    }

    [HttpGet("activity")]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<IActionResult> Activity(string? user = null, string? action = null, string? entityType = null, DateTime? date = null)
    {
        var logs = await _auditQuery.ApplyFilters(_context.AuditLogs.AsQueryable(), user, action, entityType, date)
            .OrderByDescending(x => x.Timestamp)
            .Take(50)
            .Select(x => new { x.Id, x.Timestamp, x.UserId, x.Action, x.EntityType, x.Description, x.Details, x.IpAddress })
            .ToListAsync();

        return Ok(new { logs });
    }
}
