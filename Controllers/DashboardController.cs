using MediVault.Constants;
using MediVault.Data;
using MediVault.Models;
using MediVault.Services;
using MediVault.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MediVault.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly MediVaultContext _context;
    private readonly EncryptionService _encryption;
    private readonly IntegrityService _integrity;
    private readonly RecordAccessService _recordAccess;
    private readonly AuditQueryService _auditQuery;
    private readonly PdfReportService _pdf;
    private readonly AuditService _audit;
    private readonly IEmailSender _emailSender;

    public DashboardController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        MediVaultContext context,
        EncryptionService encryption,
        IntegrityService integrity,
        RecordAccessService recordAccess,
        AuditQueryService auditQuery,
        PdfReportService pdf,
        AuditService audit,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _encryption = encryption;
        _integrity = integrity;
        _recordAccess = recordAccess;
        _auditQuery = auditQuery;
        _pdf = pdf;
        _audit = audit;
        _emailSender = emailSender;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(Roles.SystemAdministrator))
        {
            return RedirectToAction(nameof(Admin));
        }

        if (roles.Contains(Roles.Doctor))
        {
            return RedirectToAction(nameof(Doctor));
        }

        if (roles.Contains(Roles.Nurse))
        {
            return RedirectToAction(nameof(Nurse));
        }

        if (roles.Contains(Roles.RecordOfficer))
        {
            return RedirectToAction(nameof(RecordOfficer));
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<IActionResult> Admin()
    {
        await DbSeeder.EnsureSampleAuditLogsAsync(_context);

        var totalUsers = await _userManager.Users.CountAsync();
        var totalRecords = await _context.PatientRecords.CountAsync();
        var totalAuditLogs = await _context.AuditLogs.CountAsync();
        var recentAuditLogs = await _context.AuditLogs
            .OrderByDescending(x => x.Timestamp)
            .Take(10)
            .ToListAsync();

        ViewBag.TotalUsers = totalUsers;
        ViewBag.TotalRecords = totalRecords;
        ViewBag.TotalAuditLogs = totalAuditLogs;
        ViewBag.RecentAuditLogs = recentAuditLogs;

        return View();
    }

    [HttpGet]
    [Authorize(Roles = Roles.Doctor)]
    public async Task<IActionResult> Doctor()
    {
        var user = await _userManager.GetUserAsync(User);
        var patientRecords = await _context.PatientRecords
            .Where(x => x.Status != "Discharged" && x.AssignedDoctorId == user!.Id)
            .OrderByDescending(x => x.DateAdmitted)
            .ToListAsync();

        ViewBag.PatientRecords = patientRecords;
        return View();
    }

    [HttpGet]
    [Authorize(Roles = Roles.Nurse)]
    public async Task<IActionResult> Nurse()
    {
        var user = await _userManager.GetUserAsync(User);
        var patientRecords = await _context.PatientRecords
            .Where(x => x.Status != "Discharged" && x.AssignedNurseId == user!.Id)
            .OrderByDescending(x => x.DateAdmitted)
            .ToListAsync();

        ViewBag.PatientRecords = patientRecords;
        return View();
    }

    [HttpGet]
    [Authorize(Roles = Roles.RecordOfficer)]
    public async Task<IActionResult> RecordOfficer()
    {
        var patientRecords = await _context.PatientRecords
            .OrderByDescending(x => x.DateAdmitted)
            .ToListAsync();

        var appointments = await _context.Appointments
            .Include(x => x.PatientRecord)
            .OrderByDescending(x => x.ScheduledAt)
            .Take(10)
            .ToListAsync();

        var billingRecords = await _context.BillingRecords
            .Include(x => x.PatientRecord)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync();

        ViewBag.PatientRecords = patientRecords;
        ViewBag.Appointments = appointments;
        ViewBag.BillingRecords = billingRecords;
        return View();
    }



    [HttpGet]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<IActionResult> AuditLogs(string? user, string? action, string? entityType, DateTime? date, int pageNumber = 1, int pageSize = 20)
    {
        await DbSeeder.EnsureSampleAuditLogsAsync(_context);

        var normalizedAction = _auditQuery.NormalizeActionFilter(action);
        var normalizedEntityType = _auditQuery.NormalizeEntityTypeFilter(entityType);

        var allAuditLogs = await _context.AuditLogs.CountAsync();
        var query = _auditQuery.ApplyFilters(_context.AuditLogs.AsQueryable(), user?.Trim(), normalizedAction, normalizedEntityType, date);

        var filteredRecords = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)filteredRecords / pageSize));

        var auditLogs = await query
            .OrderByDescending(x => x.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return View(new AuditLogsViewModel
        {
            AuditLogs = auditLogs,
            PageNumber = pageNumber,
            TotalPages = totalPages,
            TotalAuditLogs = allAuditLogs,
            FilteredAuditLogs = filteredRecords,
            FilterUser = user?.Trim() ?? string.Empty,
            FilterAction = normalizedAction ?? string.Empty,
            FilterEntityType = normalizedEntityType ?? string.Empty,
            FilterDate = date?.ToString("yyyy-MM-dd") ?? string.Empty
        });
    }

    [HttpGet]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<IActionResult> ExportAuditLogsCsv(string? user, string? action, string? entityType, DateTime? date)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var normalizedAction = _auditQuery.NormalizeActionFilter(action);
        var normalizedEntityType = _auditQuery.NormalizeEntityTypeFilter(entityType);
        var query = _auditQuery.ApplyFilters(_context.AuditLogs.AsQueryable(), user?.Trim(), normalizedAction, normalizedEntityType, date);

        var logs = await query
            .OrderByDescending(x => x.Timestamp)
            .Take(5000)
            .ToListAsync();

        if (currentUser != null)
        {
            await _audit.LogAsync(
                currentUser.Id,
                "Download",
                "Exported audit logs CSV",
                "AuditLog",
                $"Filters: user={user?.Trim() ?? string.Empty}; action={normalizedAction ?? string.Empty}; entityType={normalizedEntityType ?? string.Empty}; date={date:yyyy-MM-dd}");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(_auditQuery.BuildCsv(logs));
        var fileName = $"medivault-audit-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<IActionResult> ExportAuditLogsPdf(string? user, string? action, string? entityType, DateTime? date)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var normalizedAction = _auditQuery.NormalizeActionFilter(action);
        var normalizedEntityType = _auditQuery.NormalizeEntityTypeFilter(entityType);
        var query = _auditQuery.ApplyFilters(_context.AuditLogs.AsQueryable(), user?.Trim(), normalizedAction, normalizedEntityType, date);

        var logs = await query
            .OrderByDescending(x => x.Timestamp)
            .Take(200)
            .ToListAsync();

        if (currentUser != null)
        {
            await _audit.LogAsync(
                currentUser.Id,
                "Download",
                "Exported audit logs PDF",
                "AuditLog",
                $"Filters: user={user?.Trim() ?? string.Empty}; action={normalizedAction ?? string.Empty}; entityType={normalizedEntityType ?? string.Empty}; date={date:yyyy-MM-dd}");
        }

        var bytes = _pdf.BuildAuditLogPdf(logs);
        var fileName = $"medivault-audit-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.Doctor},{Roles.Nurse}")]
    public async Task<IActionResult> ViewPatient(int id)
    {
        var record = await _context.PatientRecords.FindAsync(id);
        if (record == null)
        {
            return NotFound();
        }

        if (!await CanAccessRecordAsync(record))
        {
            return Forbid();
        }

        if (!await VerifyIntegrityOrBlockAsync(record))
        {
            return StatusCode(StatusCodes.Status409Conflict, "Patient record integrity verification failed.");
        }

        var user = await _userManager.GetUserAsync(User);
        await _audit.LogAsync(
            user!.Id,
            "ViewRecord",
            $"Viewed patient record #{record.Id}",
            "PatientRecord",
            $"{record.PatientName} ({record.Diagnosis})");

        var model = ToViewModel(record, decrypt: true);
        ViewBag.CanEdit = await CanEditRecordAsync(record);
        ViewBag.ReturnAction = await GetDashboardActionAsync();

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = Roles.Doctor)]
    public async Task<IActionResult> CreatePatient()
    {
        var model = new PatientRecordFormViewModel
        {
            ReturnAction = await GetDashboardActionAsync()
        };

        return View("EditPatient", model);
    }

    [HttpGet]
    [Authorize(Roles = Roles.Doctor)]
    public async Task<IActionResult> EditPatient(int id)
    {
        var record = await _context.PatientRecords.FindAsync(id);
        if (record == null)
        {
            return NotFound();
        }

        if (!await CanEditRecordAsync(record))
        {
            return Forbid();
        }

        if (!await VerifyIntegrityOrBlockAsync(record))
        {
            return StatusCode(StatusCodes.Status409Conflict, "Patient record integrity verification failed.");
        }

        var model = ToViewModel(record, decrypt: true);
        model.ReturnAction = await GetDashboardActionAsync();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Doctor)]
    public async Task<IActionResult> EditPatient(PatientRecordFormViewModel model)
    {
        model.ReturnAction ??= await GetDashboardActionAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        var isCreate = model.Id == 0;
        PatientRecord record;

        if (isCreate)
        {
            record = new PatientRecord
            {
                CreatedAt = DateTime.UtcNow,
                AssignedDoctorId = user!.Id,
                AssignedNurseId = await GetDefaultNurseIdAsync()
            };
            _context.PatientRecords.Add(record);
        }
        else
        {
            var existing = await _context.PatientRecords.FindAsync(model.Id);
            if (existing == null)
            {
                return NotFound();
            }

            if (!await CanEditRecordAsync(existing))
            {
                return Forbid();
            }

            if (!await VerifyIntegrityOrBlockAsync(existing))
            {
                return StatusCode(StatusCodes.Status409Conflict, "Patient record integrity verification failed.");
            }

            record = existing;
        }

        ApplyViewModel(record, model);
        record.UpdatedAt = DateTime.UtcNow;
        _integrity.StampPatientRecord(record);
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            user!.Id,
            isCreate ? "CreateRecord" : "UpdateRecord",
            $"{(isCreate ? "Created" : "Updated")} patient record #{record.Id}",
            "PatientRecord",
            $"{record.PatientName} - {record.Diagnosis}");

        TempData["Success"] = isCreate ? "Patient record created." : "Patient record updated.";
        return RedirectToAction(nameof(ViewPatient), new { id = record.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Nurse)]
    public async Task<IActionResult> AddCareNote(int id, string careNote)
    {
        if (string.IsNullOrWhiteSpace(careNote))
        {
            TempData["Error"] = "Care note cannot be empty.";
            return RedirectToAction(nameof(Nurse));
        }

        var record = await _context.PatientRecords.FindAsync(id);
        if (record == null)
        {
            return NotFound();
        }

        if (!await CanAddCareNoteAsync(record))
        {
            return Forbid();
        }

        if (!await VerifyIntegrityOrBlockAsync(record))
        {
            return StatusCode(StatusCodes.Status409Conflict, "Patient record integrity verification failed.");
        }

        var user = await _userManager.GetUserAsync(User);
        var timestamp = DateTime.UtcNow.ToString("g");
        var existing = string.IsNullOrEmpty(record.MedicalHistoryEncrypted)
            ? string.Empty
            : _encryption.Decrypt(record.MedicalHistoryEncrypted) + Environment.NewLine;

        var note = $"[{timestamp}] Nurse note by {user!.Email}: {careNote.Trim()}";
        record.MedicalHistoryEncrypted = _encryption.Encrypt(existing + note);
        record.UpdatedAt = DateTime.UtcNow;
        _integrity.StampPatientRecord(record);
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            user.Id,
            "UpdateRecord",
            $"Added care note to patient record #{record.Id}",
            "PatientRecord",
            careNote.Trim());

        TempData["Success"] = "Care note saved.";
        return RedirectToAction(nameof(Nurse));
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.RecordOfficer},{Roles.Doctor}")]
    public async Task<IActionResult> PrintPatient(int id)
    {
        var record = await _context.PatientRecords.FindAsync(id);
        if (record == null)
        {
            return NotFound();
        }

        if (!await CanPrintRecordAsync(record))
        {
            return Forbid();
        }

        if (!await VerifyIntegrityOrBlockAsync(record))
        {
            return StatusCode(StatusCodes.Status409Conflict, "Patient record integrity verification failed.");
        }

        var user = await _userManager.GetUserAsync(User);
        await _audit.LogAsync(
            user!.Id,
            "Download",
            $"Printed patient record #{record.Id}",
            "PatientRecord",
            record.PatientName);

        var isRedacted = User.IsInRole(Roles.RecordOfficer);
        var model = ToViewModel(record, decrypt: !isRedacted);
        if (isRedacted)
        {
            model.Diagnosis = "Restricted";
            model.MedicalHistory = "Redacted for Record Officer access.";
            model.Diagnoses = "Redacted for Record Officer access.";
            model.Prescriptions = "Redacted for Record Officer access.";
        }

        ViewBag.IsRedacted = isRedacted;
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.RecordOfficer},{Roles.Doctor}")]
    public async Task<IActionResult> ExportPatientPdf(int id)
    {
        var record = await _context.PatientRecords.FindAsync(id);
        if (record == null)
        {
            return NotFound();
        }

        if (!await CanPrintRecordAsync(record))
        {
            return Forbid();
        }

        if (!await VerifyIntegrityOrBlockAsync(record))
        {
            return StatusCode(StatusCodes.Status409Conflict, "Patient record integrity verification failed.");
        }

        var user = await _userManager.GetUserAsync(User);
        var isRedacted = User.IsInRole(Roles.RecordOfficer);
        var model = ToViewModel(record, decrypt: !isRedacted);
        if (isRedacted)
        {
            model.Diagnosis = "Restricted";
            model.MedicalHistory = "Redacted for Record Officer access.";
            model.Diagnoses = "Redacted for Record Officer access.";
            model.Prescriptions = "Redacted for Record Officer access.";
        }

        await _audit.LogAsync(
            user!.Id,
            "Download",
            $"Exported patient record PDF #{record.Id}",
            "PatientRecord",
            record.PatientName);

        var bytes = _pdf.BuildPatientRecordPdf(model, isRedacted);
        return File(bytes, "application/pdf", $"patient-record-{record.Id:D4}.pdf");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.RecordOfficer)]
    public async Task<IActionResult> ScheduleAppointment(int patientRecordId, DateTime scheduledAt, string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose) || scheduledAt == default)
        {
            TempData["Error"] = "Appointment date/time and purpose are required.";
            return RedirectToAction(nameof(RecordOfficer));
        }

        if (scheduledAt <= DateTime.Now.AddMinutes(-5))
        {
            TempData["Error"] = "Appointment must be scheduled for a future time.";
            return RedirectToAction(nameof(RecordOfficer));
        }

        if (purpose.Length > 120)
        {
            TempData["Error"] = "Appointment purpose is too long.";
            return RedirectToAction(nameof(RecordOfficer));
        }

        var record = await _context.PatientRecords.FindAsync(patientRecordId);
        if (record == null)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        var appointment = new Appointment
        {
            PatientRecordId = record.Id,
            ScheduledAt = scheduledAt,
            Purpose = purpose.Trim(),
            Status = "Scheduled",
            CreatedByUserId = user!.Id,
            CreatedAt = DateTime.UtcNow
        };

        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            user.Id,
            "ScheduleAppointment",
            $"Scheduled appointment for patient record #{record.Id}",
            "Appointment",
            $"{record.PatientName}: {appointment.Purpose} at {appointment.ScheduledAt:g}");

        TempData["Success"] = "Appointment scheduled.";
        return RedirectToAction(nameof(RecordOfficer));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.RecordOfficer)]
    public async Task<IActionResult> GenerateBill(int patientRecordId, string description, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(description) || amount <= 0)
        {
            TempData["Error"] = "Billing description and positive amount are required.";
            return RedirectToAction(nameof(RecordOfficer));
        }

        if (description.Length > 160)
        {
            TempData["Error"] = "Billing description is too long.";
            return RedirectToAction(nameof(RecordOfficer));
        }

        var record = await _context.PatientRecords.FindAsync(patientRecordId);
        if (record == null)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        var bill = new BillingRecord
        {
            PatientRecordId = record.Id,
            Description = description.Trim(),
            Amount = decimal.Round(amount, 2),
            Status = "Pending",
            CreatedByUserId = user!.Id,
            CreatedAt = DateTime.UtcNow
        };

        _context.BillingRecords.Add(bill);
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            user.Id,
            "GenerateBill",
            $"Generated bill for patient record #{record.Id}",
            "BillingRecord",
            $"{record.PatientName}: {bill.Description} - {bill.Amount:C}");

        TempData["Success"] = "Billing record generated.";
        return RedirectToAction(nameof(RecordOfficer));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.RecordOfficer)]
    public async Task<IActionResult> MarkBillPaid(int id)
    {
        var bill = await _context.BillingRecords
            .Include(x => x.PatientRecord)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (bill == null)
        {
            return NotFound();
        }

        if (bill.Status == "Paid")
        {
            TempData["Error"] = $"Bill #{bill.Id} is already paid.";
            return RedirectToAction(nameof(RecordOfficer));
        }

        bill.Status = "Paid";
        await _context.SaveChangesAsync();

        var user = await _userManager.GetUserAsync(User);
        await _audit.LogAsync(
            user!.Id,
            "UpdateBilling",
            $"Marked bill #{bill.Id} paid",
            "BillingRecord",
            bill.PatientRecord?.PatientName ?? bill.Id.ToString());

        TempData["Success"] = "Billing record marked as paid.";
        return RedirectToAction(nameof(RecordOfficer));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.RecordOfficer)]
    public async Task<IActionResult> RescheduleAppointment(int id, DateTime scheduledAt)
    {
        if (scheduledAt == default)
        {
            TempData["Error"] = "New appointment date/time is required.";
            return RedirectToAction(nameof(RecordOfficer));
        }

        if (scheduledAt <= DateTime.Now.AddMinutes(-5))
        {
            TempData["Error"] = "New appointment date/time must be in the future.";
            return RedirectToAction(nameof(RecordOfficer));
        }

        var appointment = await _context.Appointments
            .Include(x => x.PatientRecord)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (appointment == null)
        {
            return NotFound();
        }

        if (appointment.Status == "Cancelled")
        {
            TempData["Error"] = "Cancelled appointments must be recreated instead of rescheduled.";
            return RedirectToAction(nameof(RecordOfficer));
        }

        appointment.ScheduledAt = scheduledAt;
        appointment.Status = "Scheduled";
        await _context.SaveChangesAsync();

        var user = await _userManager.GetUserAsync(User);
        await _audit.LogAsync(
            user!.Id,
            "RescheduleAppointment",
            $"Rescheduled appointment #{appointment.Id}",
            "Appointment",
            $"{appointment.PatientRecord?.PatientName ?? appointment.PatientRecordId.ToString()}: {appointment.ScheduledAt:g}");

        TempData["Success"] = "Appointment rescheduled.";
        return RedirectToAction(nameof(RecordOfficer));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.RecordOfficer)]
    public async Task<IActionResult> CancelAppointment(int id)
    {
        var appointment = await _context.Appointments
            .Include(x => x.PatientRecord)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (appointment == null)
        {
            return NotFound();
        }

        if (appointment.Status == "Cancelled")
        {
            TempData["Error"] = $"Appointment #{appointment.Id} is already cancelled.";
            return RedirectToAction(nameof(RecordOfficer));
        }

        appointment.Status = "Cancelled";
        await _context.SaveChangesAsync();

        var user = await _userManager.GetUserAsync(User);
        await _audit.LogAsync(
            user!.Id,
            "CancelAppointment",
            $"Cancelled appointment #{appointment.Id}",
            "Appointment",
            appointment.PatientRecord?.PatientName ?? appointment.PatientRecordId.ToString());

        TempData["Success"] = "Appointment cancelled.";
        return RedirectToAction(nameof(RecordOfficer));
    }

    [HttpGet]
    [Authorize(Roles = Roles.RecordOfficer)]
    public async Task<IActionResult> ExportBillPdf(int id)
    {
        var bill = await _context.BillingRecords
            .Include(x => x.PatientRecord)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (bill == null)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        await _audit.LogAsync(
            user!.Id,
            "ExportInvoice",
            $"Exported invoice PDF #{bill.Id}",
            "BillingRecord",
            bill.PatientRecord?.PatientName ?? bill.PatientRecordId.ToString());

        var bytes = _pdf.BuildInvoicePdf(bill);
        return File(bytes, "application/pdf", $"invoice-{bill.Id:D6}.pdf");
    }

    [HttpGet]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<IActionResult> UserManagement()
    {
        var users = await _userManager.Users.OrderBy(x => x.Email).ToListAsync();
        var models = new List<UserManagementViewModel>();

        foreach (var user in users)
        {
            models.Add(new UserManagementViewModel
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? "unknown",
                EmailConfirmed = user.EmailConfirmed,
                IsEnabled = user.IsEnabled,
                Roles = await _userManager.GetRolesAsync(user)
            });
        }

        ViewBag.AllRoles = Roles.All;
        return View(models);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<IActionResult> ToggleUserStatus(UserStatusToggleViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (user.Id == currentUser!.Id)
        {
            TempData["Error"] = "You cannot disable your own administrator account.";
            return RedirectToAction(nameof(UserManagement));
        }

        user.IsEnabled = model.IsEnabled;
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            var action = model.IsEnabled ? "EnableUser" : "DisableUser";
            var statusText = model.IsEnabled ? "enabled" : "disabled";
            
            await _audit.LogAsync(
                currentUser.Id,
                action,
                $"{statusText.ToUpper()}: {user.Email}",
                "User",
                $"User account {statusText}");

            TempData["Success"] = $"Successfully {statusText} user account {user.Email}.";
        }
        else
        {
            TempData["Error"] = $"Failed to update user status for {user.Email}.";
        }

        return RedirectToAction(nameof(UserManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<IActionResult> AssignRole(RoleAssignmentViewModel model)
    {
        // Role assignment has been disabled for security
        TempData["Error"] = "Role assignment has been disabled. Contact system administrator.";
        return RedirectToAction(nameof(UserManagement));
    }

    [HttpGet]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public IActionResult RoleManagement()
    {
        // Role management has been disabled for security
        TempData["Error"] = "Role management has been disabled. Contact system administrator.";
        return RedirectToAction(nameof(Admin));
    }

    [HttpGet]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public IActionResult SystemSettings()
    {
        return View();
    }

    [HttpGet]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public IActionResult EncryptionKeyManagement()
    {
        return View();
    }

    [HttpGet]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public IActionResult CreateStaffUser()
    {
        ViewBag.StaffRoles = Roles.Staff;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<IActionResult> CreateStaffUser(CreateStaffUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.StaffRoles = Roles.Staff;
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "A user with this email address already exists.");
            ViewBag.StaffRoles = Roles.Staff;
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = false, // Require email verification
            IsEnabled = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, model.Role);

            // Log administrative action
            var currentUser = await _userManager.GetUserAsync(User);
            await _audit.LogAsync(
                currentUser!.Id,
                "CreateStaffUser",
                $"Created staff account for {model.Email} with role {model.Role}",
                "User",
                $"Assigned Role: {model.Role}");

            // Send verification email
            try
            {
                var userId = await _userManager.GetUserIdAsync(user);
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = userId, code = code },
                    protocol: Request.Scheme)!;

                await _emailSender.SendEmailAsync(
                    model.Email,
                    "Confirm your email",
                    $"Please confirm your account by <a href='{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
            }
            catch (Exception)
            {
                TempData["Warning"] = "Account created, but verification email could not be sent.";
            }

            TempData["Success"] = $"Successfully provisioned staff account for {model.Email}.";
            return RedirectToAction(nameof(UserManagement));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        ViewBag.StaffRoles = Roles.Staff;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (user.Id == currentUser!.Id)
        {
            TempData["Error"] = "You cannot delete your own administrator account.";
            return RedirectToAction(nameof(UserManagement));
        }

        // Optional: Require users to be disabled before deletion for safety
        if (user.IsEnabled)
        {
            TempData["Error"] = "Please disable the user account before deletion for safety.";
            return RedirectToAction(nameof(UserManagement));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var roleStr = string.Join(", ", roles);

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            await _audit.LogAsync(
                currentUser.Id,
                "DeleteUser",
                $"Deleted user account {user.Email}",
                "User",
                $"Former roles: {roleStr}");

            TempData["Success"] = $"Successfully deleted user account {user.Email}.";
        }
        else
        {
            TempData["Error"] = "Failed to delete user account.";
        }

        return RedirectToAction(nameof(UserManagement));
    }

    private async Task<bool> CanAccessRecordAsync(PatientRecord record)
    {
        return await _recordAccess.CanViewRecordAsync(User, record);
    }

    private async Task<bool> CanEditRecordAsync(PatientRecord record)
    {
        return await _recordAccess.CanEditRecordAsync(User, record);
    }

    private async Task<bool> CanAddCareNoteAsync(PatientRecord record)
    {
        return await _recordAccess.CanAddCareNoteAsync(User, record);
    }

    private async Task<bool> CanPrintRecordAsync(PatientRecord record)
    {
        return await _recordAccess.CanPrintRecordAsync(User, record);
    }

    private async Task<bool> VerifyIntegrityOrBlockAsync(PatientRecord record)
    {
        if (_integrity.VerifyPatientRecord(record))
        {
            return true;
        }

        var user = await _userManager.GetUserAsync(User);
        await _audit.LogAsync(
            user?.Id ?? "anonymous",
            "IntegrityFailure",
            $"Integrity verification failed for patient record #{record.Id}",
            "PatientRecord",
            record.PatientName);

        return false;
    }

    private async Task<string?> GetDefaultNurseIdAsync()
    {
        var nurses = await _userManager.GetUsersInRoleAsync(Roles.Nurse);
        return nurses.FirstOrDefault()?.Id;
    }

    private async Task<string> GetDashboardActionAsync()
    {
        return await _recordAccess.GetDashboardActionAsync(User);
    }

    private PatientRecordFormViewModel ToViewModel(PatientRecord record, bool decrypt)
    {
        return new PatientRecordFormViewModel
        {
            Id = record.Id,
            PatientId = record.PatientId,
            FirstName = record.FirstName,
            LastName = record.LastName,
            Age = record.Age,
            Diagnosis = record.Diagnosis,
            Status = record.Status,
            DateAdmitted = record.DateAdmitted,
            MedicalHistory = decrypt ? _encryption.Decrypt(record.MedicalHistoryEncrypted) : record.MedicalHistoryEncrypted,
            Diagnoses = decrypt ? _encryption.Decrypt(record.DiagnosesEncrypted) : record.DiagnosesEncrypted,
            Prescriptions = decrypt ? _encryption.Decrypt(record.PrescriptionsEncrypted) : record.PrescriptionsEncrypted
        };
    }

    private void ApplyViewModel(PatientRecord record, PatientRecordFormViewModel model)
    {
        record.PatientId = model.PatientId;
        record.FirstName = model.FirstName;
        record.LastName = model.LastName;
        record.Age = model.Age;
        record.Diagnosis = model.Diagnosis;
        record.Status = model.Status;
        record.DateAdmitted = model.DateAdmitted;
        record.MedicalHistoryEncrypted = _encryption.Encrypt(model.MedicalHistory);
        record.DiagnosesEncrypted = _encryption.Encrypt(model.Diagnoses);
        record.PrescriptionsEncrypted = _encryption.Encrypt(model.Prescriptions);
    }
}
