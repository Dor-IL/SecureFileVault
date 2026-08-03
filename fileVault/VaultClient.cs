using System.Net.Sockets;

namespace fileVault
{
    public class VaultClient
    {
        private TcpClient _client;
        private NetworkStream _stream;

        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);

        public async Task ConnectAsync(string host, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            Console.WriteLine("[Client] Connected to server.");
        }

        public async Task<(bool success, string message)> LoginAsync(string username, string password)
        {
            await _requestLock.WaitAsync();
            try
            {
                await NetworkHelper.SendTextAsync(_stream, $"LOGIN|{username}|{password}");
                string response = await NetworkHelper.ReceiveTextAsync(_stream);

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
                await NetworkHelper.SendTextAsync(_stream, $"REGISTER|{username}|{password}");
                string response = await NetworkHelper.ReceiveTextAsync(_stream);

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
                await NetworkHelper.SendTextAsync(_stream, $"UPLOAD|{userId}|{fileName}");
                await NetworkHelper.SendMessageAsync(_stream, fileBytes);

                string response = await NetworkHelper.ReceiveTextAsync(_stream);
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
            byte[] fileBytes;
            string fileName;
            try
            {
                await NetworkHelper.SendTextAsync(_stream, $"DOWNLOAD|{userId}|{fileId}");
                string response = await NetworkHelper.ReceiveTextAsync(_stream);
                string[] parts = response.Split('|');

                if (parts[0] != "OK")
                    return (false, parts.Length > 1 ? parts[1] : "Download failed.");

                fileName = parts[1];
                fileBytes = await NetworkHelper.ReceiveMessageAsync(_stream);
            }
            finally
            {
                _requestLock.Release();
            }

            string savePath = Path.Combine(saveDirectory, fileName);
            await File.WriteAllBytesAsync(savePath, fileBytes);

            return (true, savePath);
        }

        public async Task<(bool success, string message)> ShareFileAsync(int fileId, int ownerId, string targetUsername)
        {
            await _requestLock.WaitAsync();
            try
            {
                await NetworkHelper.SendTextAsync(_stream, $"SHARE|{fileId}|{ownerId}|{targetUsername}");
                string response = await NetworkHelper.ReceiveTextAsync(_stream);

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
                await NetworkHelper.SendTextAsync(_stream, $"UNSHARE|{fileId}|{ownerId}|{targetUsername}");
                string response = await NetworkHelper.ReceiveTextAsync(_stream);

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

        public async Task<(bool exists, string message)> CheckUserExistsAsync(int userId)
        {
            await _requestLock.WaitAsync();
            try
            {
                await NetworkHelper.SendTextAsync(_stream, $"CHECK_USER|{userId}");
                string response = await NetworkHelper.ReceiveTextAsync(_stream);

                string[] parts = response.Split('|');
                return parts[0] == "OK"
                    ? (true, "")
                    : (false, parts.Length > 1 ? parts[1] : "Your account no longer exists.");
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public NetworkStream GetStream() => _stream;

        public void Close() => _client?.Close();
    }
}
