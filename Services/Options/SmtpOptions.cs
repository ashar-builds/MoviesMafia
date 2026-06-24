namespace MoviesMafia.Services.Options;

/// <summary>SMTP settings for outbound email. Bound from the "Smtp" configuration section.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "no-reply@moviesmafia.local";
    public string FromName { get; set; } = "MoviesMafia";
    public bool EnableSsl { get; set; } = true;
}
