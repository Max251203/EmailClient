namespace EmailClient.Core.Models;

public enum EmailProtocol
{
    POP3,
    IMAP
}

public enum EmailProvider
{
    Custom,
    GstuMail,
    MailRu
}

public class EmailServerSettings
{
    public string PopServer { get; set; } = string.Empty;
    public int PopPort { get; set; }
    public string ImapServer { get; set; } = string.Empty;
    public int ImapPort { get; set; }
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool RequiresSsl { get; set; }
    public bool RequiresExplicitSsl { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public EmailProtocol Protocol { get; set; } = EmailProtocol.POP3;
}

public static class EmailProviderSettings
{
    public static EmailServerSettings GetSettings(EmailProvider provider)
    {
        return provider switch
        {
            EmailProvider.MailRu => new EmailServerSettings
            {
                PopServer = "pop.mail.ru",
                PopPort = 995,
                ImapServer = "imap.mail.ru",
                ImapPort = 993,
                SmtpServer = "smtp.mail.ru",
                SmtpPort = 465,
                RequiresSsl = true,
                RequiresExplicitSsl = false,
                DisplayName = "Mail.ru",
                Protocol = EmailProtocol.IMAP
            },
            EmailProvider.GstuMail => new EmailServerSettings
            {
                ImapServer = "mail.gstu.by",
                ImapPort = 143,  // Меняем порт на 143 (стандартный порт IMAP без SSL)
                SmtpServer = "mail.gstu.by",
                SmtpPort = 587,
                RequiresSsl = false,  // Отключаем SSL для начального подключения
                RequiresExplicitSsl = true,  // Оставляем STARTTLS
                DisplayName = "GSTU Mail",
                Protocol = EmailProtocol.IMAP
            },
            _ => new EmailServerSettings
            {
                DisplayName = "Custom Mail Server",
                Protocol = EmailProtocol.POP3
            }
        };
    }
}
