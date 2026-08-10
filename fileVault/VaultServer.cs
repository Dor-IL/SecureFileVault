using System.Net;
using System.Net.Sockets;

namespace fileVault
{
    class VaultServer
    {
        private static readonly TimeSpan PortRetryInterval = TimeSpan.FromSeconds(1);

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public VaultServer(int port)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
        }

        // Only one process can hold the port at a time, so binding it doubles as leader
        // election: if another instance is already hosting, keep retrying in the background
        // so this instance takes over automatically the moment that instance goes away.
        public void Start()
        {
            Task.Run(() => StartWithRetryAsync(_cts.Token));
        }

        private async Task StartWithRetryAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _listener.Start();
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    try
                    {
                        await Task.Delay(PortRetryInterval, token);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                }
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            Console.WriteLine($"[Server] Listening on port {((IPEndPoint)_listener.LocalEndpoint).Port}...");

            await AcceptLoopAsync(token);
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

        private static (bool allowed, string message) CheckAccountActive(int userId) =>
            UserService.GetAccountState(LoginRegister.ConnectionString, userId).Describe();

        private async Task HandleUploadAsync(NetworkStream stream, string request)
        {
            try
            {
                string[] parts = request.Split('|', 3);
                int userId = int.Parse(parts[1]);
                string fileName = parts[2];

                // The client always sends the file bytes right after the header, so they
                // must be drained from the stream even when the upload is going to be rejected.
                byte[] fileData = await NetworkHelper.ReceiveMessageAsync(stream);

                var (allowed, activeMessage) = CheckAccountActive(userId);
                if (!allowed)
                {
                    await NetworkHelper.SendTextAsync(stream, $"FAIL|{activeMessage}");
                    return;
                }

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
            try
            {
                string[] parts = request.Split('|');
                int userId = int.Parse(parts[1]);
                int fileId = int.Parse(parts[2]);

                var (allowed, activeMessage) = CheckAccountActive(userId);
                if (!allowed)
                {
                    await NetworkHelper.SendTextAsync(stream, $"FAIL|{activeMessage}");
                    return;
                }

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

                        bool success = UserService.RegisterUser(LoginRegister.ConnectionString, username, password, out string error);
                        return success ? "OK" : $"FAIL|{error ?? "Username already taken."}";
                    }

                case "CHECK_USER":
                    {
                        string[] parts = request.Split('|');
                        if (parts.Length != 2 || !int.TryParse(parts[1], out int checkUserId))
                            return "FAIL|Invalid check request.";

                        var state = UserService.GetAccountState(LoginRegister.ConnectionString, checkUserId);
                        if (state == AccountState.Active)
                            return "OK";

                        string reason = state == AccountState.Locked ? "LOCKED" : "DELETED";
                        var (_, message) = state.Describe();
                        return $"FAIL|{reason}|{message}";
                    }

                case "SHARE":
                    {
                        string[] parts = request.Split('|', 4);
                        if (parts.Length != 4)
                            return "FAIL|Invalid share request.";
                        int fileId = int.Parse(parts[1]);
                        int ownerId = int.Parse(parts[2]);
                        string targetUsername = parts[3];

                        var (allowed, activeMessage) = CheckAccountActive(ownerId);
                        if (!allowed)
                            return $"FAIL|{activeMessage}";

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

                        var (allowed, activeMessage) = CheckAccountActive(ownerId);
                        if (!allowed)
                            return $"FAIL|{activeMessage}";

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
