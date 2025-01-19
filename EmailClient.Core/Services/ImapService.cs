using EmailClient.Core.Exceptions;
using EmailClient.Core.Models;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace EmailClient.Core.Services
{
    public class ImapService
    {
        private TcpClient? _client;
        private SslStream? _sslStream;
        private int _tag = 1;
        private bool _isConnected;
        private CancellationTokenSource? _cts;

        public async Task ConnectAsync(string host, int port, bool useSsl, bool explicitSsl = false)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(host, port);

                if (useSsl)
                {
                    _sslStream = new SslStream(_client.GetStream(), false,
                        (sender, certificate, chain, errors) => true);
                    await _sslStream.AuthenticateAsClientAsync(host);
                }

                string greeting = await ReadResponseAsync();
                if (!greeting.Contains("OK"))
                {
                    throw new Exception($"Invalid server greeting: {greeting}");
                }

                _isConnected = true;
            }
            catch (Exception ex)
            {
                throw new ConnectionException("Connection failed", host, port, "IMAP",
                    ConnectionErrorType.NetworkError, ex);
            }
        }

        public async Task AuthenticateAsync(string username, string password)
        {
            try
            {
                string loginCommand = $"a{_tag} LOGIN {username} {password}\r\n";
                await SendCommandAsync(loginCommand);
                string response = await ReadResponseAsync();

                if (!response.Contains("OK"))
                {
                    throw new Exception("Authentication failed");
                }

                _tag++;
            }
            catch (Exception ex)
            {
                throw new Exception($"Authentication failed: {ex.Message}", ex);
            }
        }

        public async Task<List<EmailFolder>> GetFoldersAsync()
        {
            var folders = new List<EmailFolder>();
            try
            {
                string listCommand = $"a{_tag} LIST \"\" \"*\"\r\n";
                await SendCommandAsync(listCommand);
                string response = await ReadResponseAsync();
                _tag++;

                foreach (var line in response.Split('\n'))
                {
                    if (line.StartsWith("* LIST"))
                    {
                        var match = Regex.Match(line, @"\* LIST \((.*?)\) ""(.*?)"" ""(.*?)""");
                        if (match.Success)
                        {
                            var attributes = match.Groups[1].Value;
                            var encodedName = match.Groups[3].Value;
                            var name = DecodeImapUtf7(encodedName);

                            if (name.Equals("INBOX", StringComparison.OrdinalIgnoreCase))
                            {
                                folders.Add(new EmailFolder
                                {
                                    Name = "Входящие",
                                    Path = name,
                                    Icon = "📥"
                                });
                            }
                            else if ( name.Equals("INBOX/ToMyself", StringComparison.OrdinalIgnoreCase))
                            {
                                folders.Add(new EmailFolder
                                {
                                    Name = "Письма себе",
                                    Path = encodedName,
                                    Icon = "👤"
                                });
                            }
                            else if (attributes.Contains("\\Sent") || name.Contains("Отправленные"))
                            {
                                folders.Add(new EmailFolder
                                {
                                    Name = "Отправленные",
                                    Path = encodedName,
                                    Icon = "📤"
                                });
                            }
                            else if (attributes.Contains("\\Drafts") || name.Contains("Черновики"))
                            {
                                folders.Add(new EmailFolder
                                {
                                    Name = "Черновики",
                                    Path = encodedName,
                                    Icon = "📝"
                                });
                            }
                            else if (attributes.Contains("\\Trash") || name.Contains("Корзина"))
                            {
                                folders.Add(new EmailFolder
                                {
                                    Name = "Корзина",
                                    Path = encodedName,
                                    Icon = "🗑"
                                });
                            }
                            else if (attributes.Contains("\\Junk") || name.Contains("Спам"))
                            {
                                folders.Add(new EmailFolder
                                {
                                    Name = "Спам",
                                    Path = encodedName,
                                    Icon = "⚠"
                                });
                            }
                        }
                    }
                }

                return folders;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get folders: {ex.Message}", ex);
            }
        }

        public async Task<List<Email>> GetEmailHeadersAsync(string folderPath)
        {
            var emails = new List<Email>();
            try
            {
                string selectCommand = $"a{_tag} SELECT {folderPath}\r\n";
                await SendCommandAsync(selectCommand);
                string response = await ReadResponseAsync();
                _tag++;

                if (!response.Contains("OK"))
                {
                    throw new Exception($"Failed to select folder: {response}");
                }

                string fetchCommand = $"a{_tag} FETCH 1:* (FLAGS BODY[HEADER.FIELDS (FROM TO SUBJECT DATE)])\r\n";
                await SendCommandAsync(fetchCommand);
                response = await ReadResponseAsync();
                _tag++;

                // Парсинг заголовков писем
                var currentEmail = new Email();
                foreach (var line in response.Split('\n'))
                {
                    if (line.StartsWith("* "))
                    {
                        if (!string.IsNullOrEmpty(currentEmail.MessageId))
                        {
                            emails.Add(currentEmail);
                            currentEmail = new Email();
                        }

                        if (line.Contains("FETCH"))
                        {
                            var parts = line.Split(' ');
                            if (parts.Length > 1)
                            {
                                currentEmail.MessageId = parts[1];
                            }
                        }
                    }
                    else if (line.Contains("Subject:"))
                    {
                        currentEmail.Subject = DecodeHeaderValue(line.Replace("Subject:", "").Trim());
                    }
                    else if (line.Contains("From:"))
                    {
                        currentEmail.From = DecodeHeaderValue(line.Replace("From:", "").Trim());
                    }
                    else if (line.Contains("To:"))
                    {
                        currentEmail.To = new List<string> { DecodeHeaderValue(line.Replace("To:", "").Trim()) };
                    }
                    else if (line.Contains("Date:"))
                    {
                        if (DateTimeOffset.TryParse(line.Replace("Date:", "").Trim(), out DateTimeOffset dateOffset))
                        {
                            currentEmail.Date = dateOffset.LocalDateTime;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(currentEmail.MessageId))
                {
                    emails.Add(currentEmail);
                }

                return emails;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get email headers: {ex.Message}", ex);
            }
        }

        public async Task<Email> GetEmailContentAsync(string messageId)
        {
            try
            {
                string fetchCommand = $"a{_tag} FETCH {messageId} BODY[]\r\n";
                await SendCommandAsync(fetchCommand);
                string response = await ReadResponseAsync();
                _tag++;

                var email = new Email { MessageId = messageId };
                bool isReadingBody = false;
                StringBuilder bodyBuilder = new StringBuilder();

                foreach (var line in response.Split('\n'))
                {
                    if (line.Contains("BODY[]"))
                    {
                        isReadingBody = true;
                        continue;
                    }

                    if (isReadingBody && !line.EndsWith(")"))
                    {
                        bodyBuilder.AppendLine(line);
                    }
                }

                email.Body = bodyBuilder.ToString();
                return email;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get email content: {ex.Message}", ex);
            }
        }

        public async Task DeleteEmailAsync(string messageId)
        {
            try
            {
                string storeCommand = $"a{_tag} STORE {messageId} +FLAGS (\\Deleted)\r\n";
                await SendCommandAsync(storeCommand);
                string response = await ReadResponseAsync();
                _tag++;

                if (!response.Contains("OK"))
                {
                    throw new Exception($"Failed to mark message as deleted: {response}");
                }

                string expungeCommand = $"a{_tag} EXPUNGE\r\n";
                await SendCommandAsync(expungeCommand);
                response = await ReadResponseAsync();
                _tag++;

                if (!response.Contains("OK"))
                {
                    throw new Exception($"Failed to expunge messages: {response}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete message {messageId}: {ex.Message}", ex);
            }
        }

        private async Task SendCommandAsync(string command)
        {
            if (_sslStream == null)
                throw new InvalidOperationException("Not connected");

            byte[] data = Encoding.ASCII.GetBytes(command);
            await _sslStream.WriteAsync(data);
        }

        private async Task<string> ReadResponseAsync()
        {
            if (_sslStream == null)
                throw new InvalidOperationException("Not connected");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            byte[] buffer = new byte[4096];
            StringBuilder response = new StringBuilder();
            bool continueReading = true;

            while (continueReading && !cts.Token.IsCancellationRequested)
            {
                int bytesRead = await _sslStream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                if (bytesRead == 0) break;

                string chunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                response.Append(chunk);

                string fullResponse = response.ToString();

                if (fullResponse.Contains($"a{_tag} OK") || fullResponse.Contains($"a{_tag} NO") ||
                    (!fullResponse.Contains("LOGIN") && fullResponse.Contains("* OK")))
                {
                    continueReading = false;
                }
            }

            return response.ToString();
        }

        private string DecodeImapUtf7(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var regex = new Regex(@"&([^-]*)-");
            return regex.Replace(text, match =>
            {
                try
                {
                    if (match.Groups[1].Value.Length == 0) return "&";

                    string base64 = match.Groups[1].Value
                        .Replace(",", "/")
                        .Replace("+", "+-");

                    int pad = base64.Length % 4;
                    if (pad > 0) base64 += new string('=', 4 - pad);

                    byte[] bytes = Convert.FromBase64String(base64);
                    return Encoding.UTF8.GetString(bytes);
                }
                catch
                {
                    return match.Value;
                }
            });
        }

        private string DecodeHeaderValue(string encodedValue)
        {
            try
            {
                if (string.IsNullOrEmpty(encodedValue))
                    return string.Empty;

                var regex = new Regex(@"=\?utf-8\?[Bb]\?(.*?)\?=");
                return regex.Replace(encodedValue, match =>
                {
                    try
                    {
                        var base64Data = match.Groups[1].Value;
                        var bytes = Convert.FromBase64String(base64Data);
                        return Encoding.UTF8.GetString(bytes);
                    }
                    catch
                    {
                        return match.Value;
                    }
                }).Trim();
            }
            catch
            {
                return encodedValue;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_isConnected)
            {
                try
                {
                    await SendCommandAsync($"a{_tag} LOGOUT\r\n");
                    await ReadResponseAsync();
                }
                catch { }
                finally
                {
                    _sslStream?.Dispose();
                    _client?.Dispose();
                    _sslStream = null;
                    _client = null;
                    _isConnected = false;
                }
            }
        }
    }
}