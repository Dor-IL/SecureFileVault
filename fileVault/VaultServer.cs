using System.Net;
using System.Net.Sockets;

namespace fileVault
{
    class VaultServer
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public VaultServer(int port)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"[Server] Listening on port {((IPEndPoint)_listener.LocalEndpoint).Port}...");

            Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Console.WriteLine($"[Server] Client connected: {client.Client.RemoteEndPoint}");

                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                try
                {
                    while (client.Connected)
                    {
                        string request = await NetworkHelper.ReceiveTextAsync(stream);
                        int commandEnd = request.IndexOf('|');
                        string command = commandEnd >= 0 ? request.Substring(0, commandEnd) : request;

                        if (command == "UPLOAD")
                        {
                            await HandleUploadAsync(stream, request);
                        }
                        else if (command == "DOWNLOAD")
                        {
                            await HandleDownloadAsync(stream, request);
                        }
                        else
                        {
                            string response = await ProcessCommandAsync(request);
                            await NetworkHelper.SendTextAsync(stream, response);
                        }
                    }
                }
                catch (IOException)
                {
                    Console.WriteLine("[Server] Client disconnected.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server] Error handling client: {ex.Message}");
                }
            }
        }

        private async Task HandleUploadAsync(NetworkStream stream, string request)
        {
            try
            {
                string[] parts = request.Split('|', 3);
                int userId = int.Parse(parts[1]);
                string fileName = parts[2];

                byte[] fileData = await NetworkHelper.ReceiveMessageAsync(stream);

                int fileId = await FileService.SaveNewFileAsync(LoginRegister.ConnectionString, userId, fileName, fileData);
                await NetworkHelper.SendTextAsync(stream, $"OK|{fileId}");
            }
            catch (Exception ex)
            {
                await NetworkHelper.SendTextAsync(stream, $"FAIL|{ex.Message}");
            }
        }

        private async Task HandleDownloadAsync(NetworkStream stream, string request)
        {
            // Unlike HandleUploadAsync, this had no try/catch: a missing/corrupt stored file
            // (File.ReadAllBytesAsync throwing inside GetFileForDownloadAsync) or a malformed
            // request would throw here uncaught, which HandleClientAsync's catch turns into a
            // silently closed socket instead of a response. The client is left awaiting a reply
            // that never comes, and when the socket then drops, VaultClient.DownloadFileAsync
            // throws inside Client's async void btnDownload_Click, crashing the whole app since
            // there's no unhandled-exception handler for the UI thread. Send a clean FAIL instead.
            try
            {
                string[] parts = request.Split('|');
                int userId = int.Parse(parts[1]);
                int fileId = int.Parse(parts[2]);

                var result = await FileService.GetFileForDownloadAsync(LoginRegister.ConnectionString, fileId, userId);

                if (!result.allowed)
                {
                    await NetworkHelper.SendTextAsync(stream, $"FAIL|{result.message}");
                    return;
                }

                await NetworkHelper.SendTextAsync(stream, $"OK|{result.fileName}");
                await NetworkHelper.SendMessageAsync(stream, result.data);
            }
            catch (Exception ex)
            {
                await NetworkHelper.SendTextAsync(stream, $"FAIL|{ex.Message}");
            }
        }

        private async Task<string> ProcessCommandAsync(string request)
        {
            int commandEnd = request.IndexOf('|');
            string command = commandEnd >= 0 ? request.Substring(0, commandEnd) : request;

            switch (command)
            {
                case "LOGIN":
                    {
                        string[] parts = request.Split('|');
                        if (parts.Length != 3)
                            return "FAIL|Invalid username or password.";
                        string username = parts[1];
                        string password = parts[2];

                        var result = UserService.Login(LoginRegister.ConnectionString, username, password, out int userId);

                        switch (result)
                        {
                            case LoginResult.Success:
                                return $"OK|{userId}";
                            case LoginResult.AccountLocked:
                                return "FAIL|Account locked. Try again later.";
                            default:
                                return "FAIL|Invalid username or password.";
                        }
                    }

                case "REGISTER":
                    {
                        string[] parts = request.Split('|');
                        if (parts.Length != 3)
                            return "FAIL|Username and password cannot contain the '|' character.";
                        string username = parts[1];
                        string password = parts[2];

                        bool success = UserService.RegisterUser(LoginRegister.ConnectionString, username, password);
                        return success ? "OK" : "FAIL|Username already taken.";
                    }

                case "SHARE":
                    {
                        // Unlike LOGIN/REGISTER above, this never checked parts.Length before indexing
                        // parts[1..3]. A malformed SHARE request threw an uncaught IndexOutOfRangeException,
                        // which HandleClientAsync's catch turns into a silently dropped connection instead
                        // of a FAIL response (see the same class of bug fixed in HandleDownloadAsync).
                        string[] parts = request.Split('|', 4);
                        if (parts.Length != 4)
                            return "FAIL|Invalid share request.";
                        int fileId = int.Parse(parts[1]);
                        int ownerId = int.Parse(parts[2]);
                        string targetUsername = parts[3];

                        var (success, message) = FileService.ShareFile(LoginRegister.ConnectionString, fileId, ownerId, targetUsername);
                        return success ? $"OK|{message}" : $"FAIL|{message}";
                    }

                case "UNSHARE":
                    {
                        string[] parts = request.Split('|', 4);
                        if (parts.Length != 4)
                            return "FAIL|Invalid unshare request.";
                        int fileId = int.Parse(parts[1]);
                        int ownerId = int.Parse(parts[2]);
                        string targetUsername = parts[3];

                        var (success, message) = FileService.UnshareFile(LoginRegister.ConnectionString, fileId, ownerId, targetUsername);
                        return success ? $"OK|{message}" : $"FAIL|{message}";
                    }

                default:
                    return "FAIL|Unknown command.";
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
        }
    }
}
