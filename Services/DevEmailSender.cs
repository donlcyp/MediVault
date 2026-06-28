using Microsoft.AspNetCore.Identity.UI.Services;

namespace MediVault.Services;

public class DevEmailSender(ILogger<DevEmailSender> logger) : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        logger.LogInformation("Dev email to {Email} | {Subject}", email, subject);
        return Task.CompletedTask;
    }
}
