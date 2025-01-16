namespace EmailClient.Core.Models;

public class Email
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public bool HasHtmlContent => !string.IsNullOrEmpty(HtmlBody);
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public List<Attachment> Attachments { get; set; } = new();
    public bool IsRead { get; set; }
    public string Size { get; set; } = string.Empty;
}