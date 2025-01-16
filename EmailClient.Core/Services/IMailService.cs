using EmailClient.Core.Models;

namespace EmailClient.Core.Services;

public interface IMailService
{
    Task ConnectAsync(string host, int port, bool useSsl, bool explicitSsl = false);
    Task AuthenticateAsync(string username, string password);
    Task<List<EmailFolder>> GetFoldersAsync();
    Task<List<Email>> GetEmailsAsync(string folderPath = "INBOX");
    Task DeleteEmailAsync(string messageId);
    Task DisconnectAsync();
}