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

public class ResetAuthenticatorModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<ResetAuthenticatorModel> _logger;
    private readonly AuditService _audit;

    public ResetAuthenticatorModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<ResetAuthenticatorModel> logger,
        AuditService audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
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
                "TwoFactorResetBlocked",
                "Attempted to reset MFA for system administrator",
                "User",
                user.Email ?? user.Id);
            return Forbid();
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
                "TwoFactorResetBlocked",
                "Attempted to reset MFA for system administrator",
                "User",
                user.Email ?? user.Id);
            return Forbid();
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        var userId = await _userManager.GetUserIdAsync(user);
        _logger.LogInformation("User with ID '{UserId}' has reset their authentication app key.", user.Id);
        await _audit.LogAsync(
            userId,
            "TwoFactorReset",
            "User reset authenticator enrollment.",
            "User",
            user.Email ?? userId);

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Your authenticator app key has been reset, you will need to configure your authenticator app using the new key.";

        return RedirectToPage("./EnableAuthenticator");
    }
}
