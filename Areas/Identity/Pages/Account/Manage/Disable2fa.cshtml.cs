// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using MediVault.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MediVault.Data;
using MediVault.Services;

namespace MediVault.Areas.Identity.Pages.Account.Manage;

public class Disable2faModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<Disable2faModel> _logger;
    private readonly AuditService _audit;

    public Disable2faModel(
        UserManager<ApplicationUser> userManager,
        ILogger<Disable2faModel> logger,
        AuditService audit)
    {
        _userManager = userManager;
        _logger = logger;
        _audit = audit;
    }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGet()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        if (await _userManager.IsInRoleAsync(user, Roles.SystemAdministrator))
        {
            await _audit.LogAsync(
                user.Id,
                "TwoFactorDisableBlocked",
                $"Attempted to disable MFA for system administrator {user.Email}.",
                "User",
                user.Email ?? user.Id);
            return Forbid();
        }

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
        {
            throw new InvalidOperationException($"Cannot disable 2FA for user as it's not currently enabled.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        if (await _userManager.IsInRoleAsync(user, Roles.SystemAdministrator))
        {
            await _audit.LogAsync(
                user.Id,
                "TwoFactorDisableBlocked",
                $"Attempted to disable MFA for system administrator {user.Email}.",
                "User",
                user.Email ?? user.Id);
            return Forbid();
        }

        var disable2faResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disable2faResult.Succeeded)
        {
            throw new InvalidOperationException($"Unexpected error occurred disabling 2FA.");
        }

        _logger.LogInformation("User with ID '{UserId}' has disabled 2fa.", _userManager.GetUserId(User));
        await _audit.LogAsync(
            user.Id,
            "TwoFactorDisabled",
            $"User {user.Email} disabled authenticator-based MFA.",
            "User",
            user.Email ?? user.Id);
        StatusMessage = "2fa has been disabled. You can reenable 2fa when you setup an authenticator app";
        return RedirectToPage("./TwoFactorAuthentication");
    }
}
