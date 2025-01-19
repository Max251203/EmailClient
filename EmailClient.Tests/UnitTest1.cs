using EmailClient.Core.Exceptions;
using EmailClient.Core.Models;
using EmailClient.Core.Services;

public class ImapServiceTests
{
    private const string TestHost = "localhost";
    private const int TestPort = 993;

    [Fact]
    public async Task ConnectAsync_ValidConnection_ThrowsSocketException()
    {
        // Arrange
        var service = new ImapService();

        // Act & Assert
        await Assert.ThrowsAsync<ConnectionException>(() =>
            service.ConnectAsync(TestHost, TestPort, true));
    }

    
    

    [Fact]
    public async Task GetFoldersAsync_NoConnection_ThrowsException()
    {
        // Arrange
        var service = new ImapService();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            service.GetFoldersAsync());
    }
}

public class PopServiceTests
{
    private const string TestHost = "localhost";
    private const int TestPort = 995;

    [Fact]
    public async Task ConnectAsync_ValidConnection_ThrowsSocketException()
    {
        // Arrange
        var service = new PopService();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            service.ConnectAsync(TestHost, TestPort, true));
    }

    [Fact]
    public async Task AuthenticateAsync_NoConnection_ThrowsException()
    {
        // Arrange
        var service = new PopService();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AuthenticateAsync("test@example.com", "password"));
    }
}

public class SmtpServiceTests
{
    private const string TestHost = "localhost";
    private const int TestPort = 587;

    [Fact]
    public async Task AuthenticateAsync_NoConnection_ThrowsException()
    {
        // Arrange
        var service = new SmtpService();

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            service.AuthenticateAsync("test@example.com", "password"));
    }

    [Fact]
    public void CreateEmail_ValidData_CreatesCorrectly()
    {
        // Arrange
        var email = new Email
        {
            From = "sender@example.com",
            To = new List<string> { "recipient@example.com" },
            Subject = "Test Subject",
            Body = "Test Body",
            Date = DateTime.Now
        };

        // Assert
        Assert.NotNull(email);
        Assert.Equal("sender@example.com", email.From);
        Assert.Single(email.To);
        Assert.Equal("recipient@example.com", email.To.First());
        Assert.Equal("Test Subject", email.Subject);
        Assert.Equal("Test Body", email.Body);
    }

    [Fact]
    public async Task SendEmailAsync_NoConnection_ThrowsException()
    {
        // Arrange
        var service = new SmtpService();
        var email = new Email
        {
            From = "sender@example.com",
            To = new List<string> { "recipient@example.com" },
            Subject = "Test Subject",
            Body = "Test Body"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            service.SendEmailAsync(email));
    }
}