namespace EmailClient.Core.Exceptions;

public class ConnectionException : Exception
{
    public string Server { get; }
    public int Port { get; }
    public string? Protocol { get; }
    public ConnectionErrorType ErrorType { get; }

    public ConnectionException(string message, string server, int port, string protocol, ConnectionErrorType errorType = ConnectionErrorType.Unknown, Exception? innerException = null)
        : base(message, innerException)
    {
        Server = server;
        Port = port;
        Protocol = protocol;
        ErrorType = errorType;
    }
}

public enum ConnectionErrorType
{
    Unknown,
    Timeout,
    Authentication,
    ServerNotFound,
    SSLError,
    NetworkError,
    PortClosed
}