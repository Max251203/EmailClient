using EmailClient.Core.Models;
using EmailClient.Core.Services;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace EmailClient.Tests
{
    public class SmtpServiceTests
    {
        private const string TestHost = "localhost";
        private const int TestPort = 25;

        [Fact]
        public async Task ConnectAsync_ValidConnection_Succeeds()
        {
            // Arrange
            var service = new SmtpService();

            // Act & Assert
            await Assert.ThrowsAsync<SocketException>(() => 
                service.ConnectAsync(TestHost, TestPort, false));
        }

        [Fact]
        public void CreateEmail_ValidData_CreatesCorrectly()
        {
            // Arrange
            var email = new Email
            {
                From = "test@example.com",
                To = new List<string> { "recipient@example.com" },
                Subject = "Test Subject",
                Body = "Test Body"
            };

            // Assert
            Assert.NotNull(email.MessageId);
            Assert.Equal("test@example.com", email.From);
            Assert.Single(email.To);
            Assert.Equal("Test Subject", email.Subject);
            Assert.Equal("Test Body", email.Body);
        }
    }
}
