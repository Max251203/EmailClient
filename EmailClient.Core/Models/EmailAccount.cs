namespace EmailClient.Core.Models;

public class EmailAccount
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ImapServer { get; set; } = string.Empty;  // Добавляем IMAP настройки
    public int ImapPort { get; set; }
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool UseSsl { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
