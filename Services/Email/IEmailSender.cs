namespace MoviesMafia.Services.Email;

/// <summary>Sends transactional email (e.g. address-verification messages).</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
