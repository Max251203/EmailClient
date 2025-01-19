using EmailClient.Core.Models;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace EmailClient.Core.Services
{
    public class PopService
    {
        private TcpClient? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private Stream? _baseStream;
        private bool _isConnected;

        public async Task ConnectAsync(string host, int port, bool useSsl, bool explicitSsl = false)
        {
            if (_isConnected)
            {
                await DisconnectAsync();
            }

            try
            {
                Console.WriteLine($"Connecting to {host}:{port} (SSL: {useSsl})");

                _client = new TcpClient();
                _client.ReceiveTimeout = 10000; // 10 seconds timeout
                _client.SendTimeout = 10000;

                await _client.ConnectAsync(host, port);
                Console.WriteLine("TCP connection established");

                var networkStream = _client.GetStream();

                if (useSsl)
                {
                    Console.WriteLine("Initializing SSL connection");
                    var sslStream = new SslStream(
                        networkStream,
                        false,
                        (sender, certificate, chain, errors) => true
                    );

                    await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = host,
                        EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
                    });

                    Console.WriteLine("SSL authentication completed");
                    _baseStream = sslStream;
                }
                else
                {
                    _baseStream = networkStream;
                }

                _reader = new StreamReader(_baseStream, Encoding.UTF8);
                _writer = new StreamWriter(_baseStream, Encoding.UTF8) { AutoFlush = true };

                // Ждем приветственное сообщение сервера
                await Task.Delay(1000); // Небольшая задержка для стабильности
                string? response = await _reader.ReadLineAsync();
                Console.WriteLine($"Server response: {response}");

                if (string.IsNullOrEmpty(response) || !response.StartsWith("+OK"))
                {
                    throw new Exception($"Invalid server response: {response ?? "No response"}");
                }

                _isConnected = true;
                Console.WriteLine("Connection established successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex}");
                await CleanupAsync();
                throw new Exception($"Failed to connect to {host}:{port}. {ex.Message}", ex);
            }
        }

        public async Task AuthenticateAsync(string username, string password)
        {
            try
            {
                Console.WriteLine($"Authenticating user: {username}");
                await SendCommandAsync($"USER {username}");
                await SendCommandAsync($"PASS {password}");
                Console.WriteLine("Authentication successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication error: {ex}");
                throw new Exception($"Authentication failed for {username}. {ex.Message}", ex);
            }
        }

        public async Task<List<Email>> GetEmailsAsync()
        {
            var emails = new List<Email>();
            try
            {
                Console.WriteLine("Retrieving email list");
                string response = await SendCommandAsync("LIST");

                string? line;
                while ((line = await _reader!.ReadLineAsync()) != ".")
                {
                    if (string.IsNullOrEmpty(line) || line.StartsWith("+OK")) continue;

                    var messageNum = line.Split(' ')[0];
                    Console.WriteLine($"Retrieving email {messageNum}");
                    var email = await RetrieveEmailAsync(messageNum);
                    if (email != null)
                    {
                        emails.Add(email);
                    }
                }

                Console.WriteLine($"Retrieved {emails.Count} emails");
                return emails;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving emails: {ex}");
                throw new Exception($"Failed to retrieve emails: {ex.Message}", ex);
            }
        }

        private async Task<Email> RetrieveEmailAsync(string messageNum)
        {
            await SendCommandAsync($"RETR {messageNum}");

            var email = new Email { MessageId = messageNum };
            var headersDone = false;
            var bodyBuilder = new StringBuilder();

            string? line;
            while ((line = await _reader!.ReadLineAsync()) != ".")
            {
                if (line.StartsWith("+OK")) continue;

                if (string.IsNullOrEmpty(line) && !headersDone)
                {
                    headersDone = true;
                    continue;
                }

                if (!headersDone)
                {
                    ProcessHeader(line, email);
                }
                else
                {
                    bodyBuilder.AppendLine(line);
                }
            }

            email.Body = bodyBuilder.ToString().Trim();
            return email;
        }

        private void ProcessHeader(string line, Email email)
        {
            try
            {
                if (line.StartsWith("From: ", StringComparison.OrdinalIgnoreCase))
                    email.From = ExtractEmail(line.Substring(6));
                else if (line.StartsWith("To: ", StringComparison.OrdinalIgnoreCase))
                    email.To = ExtractEmails(line.Substring(4));
                else if (line.StartsWith("Subject: ", StringComparison.OrdinalIgnoreCase))
                    email.Subject = line.Substring(9);
                else if (line.StartsWith("Date: ", StringComparison.OrdinalIgnoreCase))
                    email.Date = ParseDate(line.Substring(6));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing header line '{line}': {ex.Message}");
            }
        }

        private string ExtractEmail(string input)
        {
            var match = Regex.Match(input, @"[\w\.-]+@[\w\.-]+\.\w+");
            return match.Success ? match.Value : input.Trim();
        }

        private List<string> ExtractEmails(string input)
        {
            var matches = Regex.Matches(input, @"[\w\.-]+@[\w\.-]+\.\w+");
            return matches.Select(m => m.Value).ToList();
        }

        private DateTime ParseDate(string dateStr)
        {
            return DateTime.TryParse(dateStr, out DateTime result) ? result : DateTime.UtcNow;
        }

        private async Task<string> SendCommandAsync(string command)
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Not connected to server");
            }

            try
            {
                var commandToLog = command.StartsWith("PASS") ? "PASS ****" : command;
                Console.WriteLine($"Sending command: {commandToLog}");

                await _writer!.WriteLineAsync(command);
                string response = await _reader!.ReadLineAsync() ?? string.Empty;

                var responseToLog = command.StartsWith("PASS") ? "****" : response;
                Console.WriteLine($"Server response: {responseToLog}");

                if (!response.StartsWith("+OK"))
                    throw new Exception($"Server returned error: {response}");

                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command error: {ex}");
                throw new Exception($"Command failed: {ex.Message}", ex);
            }
        }

        public async Task DeleteEmailAsync(string messageId)
        {
            try
            {
                await SendCommandAsync($"DELE {messageId}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete message {messageId}: {ex.Message}", ex);
            }
        }

        private async Task CleanupAsync()
        {
            try
            {
                _reader?.Dispose();
                _writer?.Dispose();
                _baseStream?.Dispose();
                _client?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup error: {ex}");
            }
            finally
            {
                _reader = null;
                _writer = null;
                _baseStream = null;
                _client = null;
                _isConnected = false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_isConnected)
            {
                try
                {
                    Console.WriteLine("Disconnecting from server");
                    await SendCommandAsync("QUIT");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during disconnect: {ex}");
                }
                finally
                {
                    await CleanupAsync();
                }
            }
        }
    }
}