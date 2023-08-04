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
    public class HeartBeat
    {
        private readonly string serverUrl;
        private readonly int pingInterval;
        private readonly int responseTimeout;

        private CancellationTokenSource cancellationTokenSource;
        private HttpClient httpClient;

        public HeartBeat(string serverUrl, int pingIntervalSeconds, int responseTimeoutSeconds)
        {
            this.serverUrl = serverUrl;
            this.pingInterval = pingIntervalSeconds * 1000;
            this.responseTimeout = responseTimeoutSeconds * 1000;

            this.httpClient = new HttpClient();
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await SendPingAsync();
                    await Task.Delay(pingInterval, cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {

                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");

                }
            }
        }

        public async Task SendPingAsync()
        {
            Console.WriteLine("Sending ping...");
            HttpResponseMessage response = await httpClient.GetAsync(serverUrl, cancellationTokenSource.Token);
            response.EnsureSuccessStatusCode();

            Console.WriteLine("Ping received. Waiting for pong...");
            await Task.Delay(responseTimeout, cancellationTokenSource.Token);
            Console.WriteLine("No pong received within timeout.");
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            httpClient.Dispose();
        }


    }
}
