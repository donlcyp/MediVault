using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;

namespace MediVault.Services;

public class GmailEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<GmailEmailSender> _logger;

    public GmailEmailSender(IConfiguration config, ILogger<GmailEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var gmailUser = _config["Gmail:SenderEmail"];
        var gmailAppPassword = _config["Gmail:AppPassword"];
        var senderName = _config["Gmail:SenderName"] ?? "MediVault";

        if (string.IsNullOrEmpty(gmailUser) || string.IsNullOrEmpty(gmailAppPassword))
        {
            _logger.LogWarning("Gmail SMTP credentials are not configured. Email to {Email} was not sent.", email);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, gmailUser));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlMessage
        };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(gmailUser, gmailAppPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {Email} | Subject: {Subject}", email, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} via Gmail SMTP.", email);
            throw;
        }
    }
}
