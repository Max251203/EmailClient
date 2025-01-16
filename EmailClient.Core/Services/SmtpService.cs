using EmailClient.Core.Models;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace EmailClient.Core.Services
{
    public class SmtpService
    {
        private TcpClient? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;

        public async Task ConnectAsync(string host, int port, bool useSsl)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);

            NetworkStream stream = _client.GetStream();
            if (useSsl)
            {
                var sslStream = new SslStream(
                    stream,
                    false,
                    new RemoteCertificateValidationCallback((sender, certificate, chain, errors) => true)
                );

                await sslStream.AuthenticateAsClientAsync(host);
                _reader = new StreamReader(sslStream);
                _writer = new StreamWriter(sslStream) { AutoFlush = true };
            }
            else
            {
                _reader = new StreamReader(stream);
                _writer = new StreamWriter(stream) { AutoFlush = true };
            }

            string response = await _reader.ReadLineAsync() ?? string.Empty;
            if (!response.StartsWith("220"))
                throw new Exception($"SMTP connection failed: {response}");

            await SendCommandAsync($"EHLO {host}");
        }

        public async Task AuthenticateAsync(string username, string password)
        {
            await SendCommandAsync("AUTH LOGIN");
            await SendCommandAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(username)));
            await SendCommandAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(password)));
        }

        public async Task SendEmailAsync(Email email)
        {
            try
            {
                await SendCommandAsync($"MAIL FROM:<{email.From}>");

                foreach (var recipient in email.To)
                {
                    await SendCommandAsync($"RCPT TO:<{recipient}>");
                }

                await SendCommandAsync("DATA");

                // Отправка заголовков
                await _writer!.WriteLineAsync($"From: {email.From}");
                await _writer.WriteLineAsync($"To: {string.Join(", ", email.To)}");
                await _writer.WriteLineAsync($"Subject: {email.Subject}");
                await _writer.WriteLineAsync($"Date: {email.Date:R}");
                await _writer.WriteLineAsync("MIME-Version: 1.0");

                if (email.Attachments.Any())
                {
                    string boundary = "---=" + Guid.NewGuid().ToString("N");
                    await _writer.WriteLineAsync($"Content-Type: multipart/mixed; boundary={boundary}");
                    await _writer.WriteLineAsync();

                    // Текст письма
                    await _writer.WriteLineAsync($"--{boundary}");
                    await _writer.WriteLineAsync("Content-Type: text/plain; charset=utf-8");
                    await _writer.WriteLineAsync();
                    await _writer.WriteLineAsync(email.Body);

                    // Вложения
                    foreach (var attachment in email.Attachments)
                    {
                        await _writer.WriteLineAsync($"--{boundary}");
                        await _writer.WriteLineAsync($"Content-Type: {attachment.ContentType}");
                        await _writer.WriteLineAsync($"Content-Disposition: attachment; filename={attachment.FileName}");
                        await _writer.WriteLineAsync("Content-Transfer-Encoding: base64");
                        await _writer.WriteLineAsync();

                        string base64Content = Convert.ToBase64String(attachment.Content);
                        for (int i = 0; i < base64Content.Length; i += 76)
                        {
                            int length = Math.Min(76, base64Content.Length - i);
                            await _writer.WriteLineAsync(base64Content.Substring(i, length));
                        }
                    }

                    await _writer.WriteLineAsync($"--{boundary}--");
                }
                else
                {
                    await _writer.WriteLineAsync("Content-Type: text/plain; charset=utf-8");
                    await _writer.WriteLineAsync();
                    await _writer.WriteLineAsync(email.Body);
                }

                await SendCommandAsync(".");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send email: {ex.Message}", ex);
            }
        }

        private async Task SendCommandAsync(string command)
        {
            await _writer!.WriteLineAsync(command);
            string response = await _reader!.ReadLineAsync() ?? string.Empty;

            // SMTP успешные коды начинаются с 2 или 3
            if (!response.StartsWith("2") && !response.StartsWith("3"))
                throw new Exception($"SMTP command '{command}' failed: {response}");
        }

        public async Task DisconnectAsync()
        {
            if (_client != null)
            {
                try
                {
                    await SendCommandAsync("QUIT");
                }
                finally
                {
                    _client.Close();
                }
            }
        }
    }
}