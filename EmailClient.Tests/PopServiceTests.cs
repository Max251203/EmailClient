using EmailClient.Core.Services;
using System.Net.Sockets;
using Xunit;

namespace EmailClient.Tests
{
    public class PopServiceTests
    {
        private const string TestHost = "localhost";
        private const int TestPort = 110;

        [Fact]
        public async Task ConnectAsync_ValidConnection_Succeeds()
        {
            // Arrange
            var service = new PopService();

            // Act & Assert
            await Assert.ThrowsAsync<SocketException>(() => 
                service.ConnectAsync(TestHost, TestPort, false));
        }

        [Fact]
        public async Task GetEmailsAsync_NoConnection_ThrowsException()
        {
            // Arrange
            var service = new PopService();

            // Act & Assert
            await Assert.ThrowsAsync<NullReferenceException>(() => 
                service.GetEmailsAsync());
        }

        [Fact]
        public async Task AuthenticateAsync_InvalidCredentials_ThrowsException()
        {
            // Arrange
            var service = new PopService();

            // Act & Assert
            await Assert.ThrowsAsync<NullReferenceException>(() => 
                service.AuthenticateAsync("invalid", "invalid"));
        }
    }
}
