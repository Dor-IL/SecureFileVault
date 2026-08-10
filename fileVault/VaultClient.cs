using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace fileVault
{
    public class VaultClient
    {
        private const int MaxAttempts = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

        private TcpClient _client;
        private Stream _stream;
        private string _host;
        private int _port;

        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);

        public async Task ConnectAsync(string host, int port)
        {
            _host = host;
            _port = port;
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = await EstablishTlsAsync(_client, host);
            Console.WriteLine("[Client] Connected to server.");
        }

        private static async Task<SslStream> EstablishTlsAsync(TcpClient client, string host)
        {
            var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false, ValidateServerCertificate);
            await sslStream.AuthenticateAsClientAsync(host);
            return sslStream;
        }

        // The server generates a self-signed certificate on first run and caches it locally
        // (see TlsCertificateProvider). Since there's no real CA, trust is established by
        // pinning: the client reads that same cached certificate and only accepts a
        // connection if the server presents the exact same one.
        private static bool ValidateServerCertificate(
            object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (certificate is null)
                return false;

            X509Certificate2 expected = TlsCertificateProvider.GetOrCreateServerCertificate();
            using var presented = new X509Certificate2(certificate);
            return string.Equals(presented.Thumbprint, expected.Thumbprint, StringComparison.OrdinalIgnoreCase);
        }

        // If the process hosting the server closed (another instance takes over the
        // listener automatically, see VaultServer), a request can land on a dead
        // connection. Retry a few times, reconnecting in between, before giving up.
        private async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (IsConnectionException(ex))
                {
                    lastException = ex;
                    if (attempt == MaxAttempts)
                    {
                        break;
                    }

                    await Task.Delay(RetryDelay);
                    await TryReconnectAsync();
                }
            }

            throw lastException;
        }

        private static bool IsConnectionException(Exception ex) =>
            ex is IOException || ex is SocketException || ex is ObjectDisposedException;

        private async Task TryReconnectAsync()
        {
            try
            {
                _client?.Close();
                _client = new TcpClient();
                await _client.ConnectAsync(_host, _port);
                _stream = await EstablishTlsAsync(_client, _host);
                Console.WriteLine("[Client] Reconnected to server.");
            }
            catch
            {
                // Next attempt (if any remain) will try again.
            }
        }

        public async Task<(bool success, string message)> LoginAsync(string username, string password)
        {
            await _requestLock.WaitAsync();
            try
            {
                string response = await ExecuteAsync(async () =>
                {
                    await NetworkHelper.SendTextAsync(_stream, $"LOGIN|{username}|{password}");
                    return await NetworkHelper.ReceiveTextAsync(_stream);
                });

                string[] parts = response.Split('|');
                if (parts[0] == "OK")
                    return (true, parts.Length > 1 ? parts[1] : "");
                else
                    return (false, parts.Length > 1 ? parts[1] : "Login failed.");
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public async Task<(bool success, string message)> RegisterAsync(string username, string password)
        {
            await _requestLock.WaitAsync();
            try
            {
                string response = await ExecuteAsync(async () =>
                {
                    await NetworkHelper.SendTextAsync(_stream, $"REGISTER|{username}|{password}");
                    return await NetworkHelper.ReceiveTextAsync(_stream);
                });

                string[] parts = response.Split('|', 2);
                return parts[0] == "OK"
                    ? (true, "Registered successfully.")
                    : (false, parts.Length > 1 ? parts[1] : "Registration failed.");
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public async Task<(bool success, string message)> UploadFileAsync(int userId, string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);

            await _requestLock.WaitAsync();
            try
            {
                string response = await ExecuteAsync(async () =>
                {
                    await NetworkHelper.SendTextAsync(_stream, $"UPLOAD|{userId}|{fileName}");
                    await NetworkHelper.SendMessageAsync(_stream, fileBytes);
                    return await NetworkHelper.ReceiveTextAsync(_stream);
                });

                string[] parts = response.Split('|');
                return parts[0] == "OK"
                    ? (true, "Upload successful.")
                    : (false, parts.Length > 1 ? parts[1] : "Upload failed.");
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public async Task<(bool success, string message)> DownloadFileAsync(int userId, int fileId, string saveDirectory)
        {
            await _requestLock.WaitAsync();
            (bool success, string fileNameOrMessage, byte[] fileBytes) result;
            try
            {
                result = await ExecuteAsync(async () =>
                {
                    await NetworkHelper.SendTextAsync(_stream, $"DOWNLOAD|{userId}|{fileId}");
                    string response = await NetworkHelper.ReceiveTextAsync(_stream);
                    string[] parts = response.Split('|');

                    if (parts[0] != "OK")
                        return (false, parts.Length > 1 ? parts[1] : "Download failed.", (byte[])null);

                    string fileName = parts[1];
                    byte[] fileBytes = await NetworkHelper.ReceiveMessageAsync(_stream);
                    return (true, fileName, fileBytes);
                });
            }
            finally
            {
                _requestLock.Release();
            }

            if (!result.success)
                return (false, result.fileNameOrMessage);

            string savePath = Path.Combine(saveDirectory, result.fileNameOrMessage);
            await File.WriteAllBytesAsync(savePath, result.fileBytes);

            return (true, savePath);
        }

        public async Task<(bool success, string message)> ShareFileAsync(int fileId, int ownerId, string targetUsername)
        {
            await _requestLock.WaitAsync();
            try
            {
                string response = await ExecuteAsync(async () =>
                {
                    await NetworkHelper.SendTextAsync(_stream, $"SHARE|{fileId}|{ownerId}|{targetUsername}");
                    return await NetworkHelper.ReceiveTextAsync(_stream);
                });

                string[] parts = response.Split('|');
                return parts[0] == "OK"
                    ? (true, parts.Length > 1 ? parts[1] : "Shared successfully.")
                    : (false, parts.Length > 1 ? parts[1] : "Share failed.");
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public async Task<(bool success, string message)> UnshareFileAsync(int fileId, int ownerId, string targetUsername)
        {
            await _requestLock.WaitAsync();
            try
            {
                string response = await ExecuteAsync(async () =>
                {
                    await NetworkHelper.SendTextAsync(_stream, $"UNSHARE|{fileId}|{ownerId}|{targetUsername}");
                    return await NetworkHelper.ReceiveTextAsync(_stream);
                });

                string[] parts = response.Split('|');
                return parts[0] == "OK"
                    ? (true, parts.Length > 1 ? parts[1] : "Unshared successfully.")
                    : (false, parts.Length > 1 ? parts[1] : "Unshare failed.");
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public async Task<(bool ok, bool locked, string message)> CheckAccountStatusAsync(int userId)
        {
            await _requestLock.WaitAsync();
            try
            {
                string response = await ExecuteAsync(async () =>
                {
                    await NetworkHelper.SendTextAsync(_stream, $"CHECK_USER|{userId}");
                    return await NetworkHelper.ReceiveTextAsync(_stream);
                });

                string[] parts = response.Split('|', 3);
                if (parts[0] == "OK")
                    return (true, false, "");

                bool locked = parts.Length > 1 && parts[1] == "LOCKED";
                string message = parts.Length > 2 ? parts[2] : "Your account is no longer available.";
                return (false, locked, message);
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public Stream GetStream() => _stream;

        public void Close() => _client?.Close();
    }
}
