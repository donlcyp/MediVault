using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MediVault.Data;

namespace MediVault.Services;

public class CustomSignInManager : SignInManager<ApplicationUser>
{
    public CustomSignInManager(
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
    }

    public override async Task<SignInResult> CheckPasswordSignInAsync(ApplicationUser user, string password, bool lockoutOnFailure)
    {
        // Check if the user account is enabled
        if (!user.IsEnabled)
        {
            Logger.LogWarning("User {Email} attempted to log in but account is disabled", user.Email);
            return SignInResult.NotAllowed;
        }

        // Call the base implementation for normal password checking
        return await base.CheckPasswordSignInAsync(user, password, lockoutOnFailure);
    }

    public override async Task<SignInResult> PasswordSignInAsync(string userName, string password, bool isPersistent, bool lockoutOnFailure)
    {
        var user = await UserManager.FindByEmailAsync(userName)
            ?? await UserManager.FindByNameAsync(userName);
        if (user == null)
        {
            return SignInResult.Failed;
        }

        // Check if the user account is enabled before proceeding
        if (!user.IsEnabled)
        {
            Logger.LogWarning("User {Email} attempted to log in but account is disabled", user.Email);
            return SignInResult.NotAllowed;
        }

        return await base.PasswordSignInAsync(userName, password, isPersistent, lockoutOnFailure);
    }

    public override async Task<SignInResult> PasswordSignInAsync(ApplicationUser user, string password, bool isPersistent, bool lockoutOnFailure)
    {
        // Check if the user account is enabled before proceeding
        if (!user.IsEnabled)
        {
            Logger.LogWarning("User {Email} attempted to log in but account is disabled", user.Email);
            return SignInResult.NotAllowed;
        }

        return await base.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);
    }
}