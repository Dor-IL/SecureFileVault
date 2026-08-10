using System.Text;

namespace fileVault
{
    static class NetworkHelper
    {
        public static async Task SendMessageAsync(Stream stream, byte[] payload)
        {
            byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);
            await stream.WriteAsync(lengthPrefix, 0, 4);
            await stream.WriteAsync(payload, 0, payload.Length);
        }

        public static Task SendTextAsync(Stream stream, string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            return SendMessageAsync(stream, payload);
        }

        public static async Task<byte[]> ReceiveMessageAsync(Stream stream)
        {
            byte[] lengthBuffer = await ReadExactAsync(stream, 4);
            int length = BitConverter.ToInt32(lengthBuffer, 0);

            if (length < 0 || length > 100_000_000)
                throw new InvalidDataException($"Invalid frame length: {length}");

            return await ReadExactAsync(stream, length);
        }

        public static async Task<string> ReceiveTextAsync(Stream stream)
        {
            byte[] payload = await ReceiveMessageAsync(stream);
            return Encoding.UTF8.GetString(payload);
        }

        private static async Task<byte[]> ReadExactAsync(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int totalRead = 0;

            while (totalRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, totalRead, count - totalRead);
                if (bytesRead == 0)
                    throw new IOException("Connection closed before expected data was received.");

                totalRead += bytesRead;
            }

            return buffer;
        }
    }
}
