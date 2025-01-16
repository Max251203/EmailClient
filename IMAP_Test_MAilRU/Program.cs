using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        string server = "imap.mail.ru";
        int port = 993; // IMAP с SSL
        string username = "maksmd251203@mail.ru"; // Ваш почтовый адрес
        string password = "BeZr6SrQrCe829zp0u3S"; // Ваш пароль (пароль для приложения)

        // MRh6iu99iLzYcyWNgzcM - для второй почты  - max.murashko251203@mail.ru


        try
        {
            using (TcpClient client = new TcpClient(server, port))
            using (SslStream sslStream = new SslStream(client.GetStream()))
            {
                sslStream.AuthenticateAsClient(server);
                Console.WriteLine("SSL-соединение установлено.");

                // Читаем приветственное сообщение от сервера
                string serverResponse = ReadResponse(sslStream);
                Console.WriteLine("Сервер: " + serverResponse);

                // Отправляем команду LOGIN
                string loginCommand = $"a1 LOGIN {username} {password}\r\n";
                SendCommand(sslStream, loginCommand);

                // Читаем ответ на LOGIN
                serverResponse = ReadResponse(sslStream);
                Console.WriteLine("Ответ на LOGIN: " + serverResponse);

                // Отправляем команду LIST для проверки папок
                if (serverResponse.Contains("OK"))
                {
                    string listCommand = "a2 LIST \"\" \"*\"\r\n";
                    SendCommand(sslStream, listCommand);

                    // Читаем ответ сервера на LIST
                    serverResponse = ReadResponse(sslStream);
                    Console.WriteLine("Список папок: " + serverResponse);
                }

                // Завершаем сессию
                string logoutCommand = "a3 LOGOUT\r\n";
                SendCommand(sslStream, logoutCommand);
                serverResponse = ReadResponse(sslStream);
                Console.WriteLine("Ответ на LOGOUT: " + serverResponse);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка: " + ex.Message);
        }
    }

    static void SendCommand(SslStream stream, string command)
    {
        byte[] data = Encoding.ASCII.GetBytes(command);
        stream.Write(data, 0, data.Length);
        Console.WriteLine("Клиент: " + command.Trim());
    }

    static string ReadResponse(SslStream stream)
    {
        byte[] buffer = new byte[1024];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        return Encoding.ASCII.GetString(buffer, 0, bytesRead);
    }
}