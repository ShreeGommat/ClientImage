using System.Net.WebSockets;
using System.Text;
using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Win32;
using System.Diagnostics;
using Timer = System.Timers.Timer;
using System.Data.SQLite;
using System.Drawing.Imaging;
using System.Net.WebSockets;
using System.Text;

namespace ClientApp
{
    public class Configuration
    {
        private ClientWebSocket webSocket;
        private string serverUrl = "ws://localhost:8080";
        private CancellationTokenSource cancellation;

        public Configuration()
        {
            InitializeWebSocket();
        }

        public async Task StartListeningAsync()
        {
            if (webSocket.State != WebSocketState.Open)
            {
                Console.WriteLine("WebSocket connection not established.");
                return;
            }

            try
            {
                byte[] buffer = new byte[1024];
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation.Token);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string configData = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"Received configuration: {configData}");

                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("WebSocket listening canceled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
            }
        }

        private async Task InitializeWebSocket()
        {
            webSocket = new ClientWebSocket();
            cancellation = new CancellationTokenSource();

            try
            {
                await webSocket.ConnectAsync(new Uri(serverUrl), cancellation.Token);
                Console.WriteLine("WebSocket connected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket connection error: {ex.Message}");
            }
        }

        public void CloseWebSocket()
        {
            cancellation.Cancel();
            webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait();
        }
    }
}
