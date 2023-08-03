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
    class ScreenCapture
    {
        private static Timer screenshotTimer;
        private static string serverUrl = "https://localhost:8090";
        private static bool isLocked = false;
        private static DBQueue dbQueue;
        private static ImageSender imageSender;

        static async Task Main(string[] args)
        {
            // Initialize and start the screenshot timer
            screenshotTimer = new Timer();
            screenshotTimer.Interval = TimeSpan.FromSeconds(3).TotalMilliseconds; 
            HeartBeat heartBeat = new HeartBeat("https://localhost:8090", 10, 20); //10 seconds, response timeout: 20 seconds
            Task heartBeatTask = heartBeat.StartAsync();
            string databaseFilePath = "C:/Users/ankit/Desktop/database/database/Images.db"; 
            string connectionString = $"Data Source={databaseFilePath};Version=3;";
            dbQueue = new DBQueue(connectionString, "C:/Users/ankit/Desktop/images");



            screenshotTimer.Elapsed += async (sender, e) =>
            {
                if (!isLocked)
                {
                    Bitmap screenshot = CaptureScreen();
                    string imagePath = SaveScreenshot(screenshot);
                    byte[] screenshotBytes = ImageToByteArray(screenshot);

                    Console.WriteLine($"Screenshot saved: {imagePath}");

                    
                    await dbQueue.InsertImageAsync(screenshotBytes);
                }
            };
            screenshotTimer.Start();

            // Subscribe to user logon/unlock events
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

            // Keep the console application running
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();

            // Clean up
            screenshotTimer.Stop();
            screenshotTimer.Dispose();
        }

        

        static Bitmap CaptureScreen()
        {
            Rectangle screenBounds = new Rectangle(0, 0, 1920, 1080); 
            Bitmap screenshot = new Bitmap(screenBounds.Width, screenBounds.Height);
            using (Graphics graphics = Graphics.FromImage(screenshot))
            {
                graphics.CopyFromScreen(screenBounds.Location, Point.Empty, screenBounds.Size);
            }
            return screenshot;
        }

        static byte[] ImageToByteArray(Image image)
        {
            using (var stream = new System.IO.MemoryStream())
            {
                image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }
        static string SaveScreenshot(Bitmap screenshot)
        {
            string imageName = $"screenshot_{DateTime.Now:yyyyMMddHHmmssfff}.jpeg";
            string imagePath = Path.Combine("C:/Users/ankit/Desktop/images" + imageName);

            screenshot.Save(imagePath, ImageFormat.Jpeg);
            return imagePath;
        }


        static void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                case SessionSwitchReason.SessionLogoff:
                    isLocked = true;
                    Console.WriteLine("Machine locked or logged off.");
                    break;

                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.SessionLogon:
                    isLocked = false;
                    Console.WriteLine("Machine unlocked or logged on.");
                    break;
            }
        }
    }







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
            this.pingInterval = pingIntervalSeconds * 1000; // milliseconds
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







    public  class DBQueue
    {
        private readonly string connectionString;
        private readonly string imagesFolder;

        public DBQueue(string connectionString, string imagesFolder)
        {
            this.connectionString = connectionString;
            this.imagesFolder = imagesFolder;

            // Ensure the images folder exists
            Directory.CreateDirectory(imagesFolder);
        }

        public async Task<bool> InsertImageAsync(byte[] imageData)
        {
            try
            {   

                string imageName = GenerateImageName();
                string imagePath = Path.Combine(imagesFolder, imageName);

                await File.WriteAllBytesAsync(imagePath, imageData);

                using SQLiteConnection connection = new SQLiteConnection(connectionString);
                await connection.OpenAsync();

                string query = "INSERT INTO Images (ImagePath, Timestamp) VALUES (@ImagePath, @Timestamp)";
                using SQLiteCommand command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@ImagePath", imageName);
                command.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
                await command.ExecuteNonQueryAsync();

                return true; // Insertion 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting image: {ex.Message}");
                return false; // Insertion failed
            }


        }
        public string GenerateImageName()
        {
            return $"{Guid.NewGuid():N}.jpeg";
        }
        public async Task<int> GetOldestImageIdAsync()
        {
            string connectionString = null;
            using SQLiteConnection connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();

            string query = "SELECT MIN(ImageId) FROM Images";
            using SQLiteCommand command = new SQLiteCommand(query, connection);
            object result = await command.ExecuteScalarAsync();

            if (result != null && result != DBNull.Value)
            {
                return Convert.ToInt32(result);
            }

            return -1;
        }
        public async Task<byte[]> GetImageAsync(int imageId)
        {
            using SQLiteConnection connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();

            string query = "SELECT ImagePath FROM Images WHERE ImageId = @ImageId";
            using SQLiteCommand command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@ImageId", imageId);
            string imageName = await command.ExecuteScalarAsync() as string;

            if (!string.IsNullOrEmpty(imageName))
            {
                string imagePath = Path.Combine(imagesFolder, imageName);
                if (File.Exists(imagePath))
                {
                    return await File.ReadAllBytesAsync(imagePath);
                }
            }

            return null;
        }

        public async Task DeleteImageAsync(int imageId)
        {
            using SQLiteConnection connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();

            string query = "DELETE FROM Images WHERE ImageId = @ImageId";
            using SQLiteCommand command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@ImageId", imageId);
            await command.ExecuteNonQueryAsync();
        }



    }







    public class ImageSender
    {
        private readonly DBQueue dbQueue;
        private readonly string serverUrl;
        private readonly int maxRetries;

        public ImageSender(DBQueue dbQueue, string serverUrl, int maxRetries)
        {
            this.dbQueue = dbQueue;
            this.serverUrl = serverUrl;
            this.maxRetries = maxRetries;
        }
                
        public async Task SendImagesAsync()
        {
            while (true)
            {
                try
                {
                    Console.WriteLine("Trying to send to ");
                    int imageId = await dbQueue.GetOldestImageIdAsync();
                    if (imageId == -1)
                    {
                        await Task.Delay(5000);
                        continue;
                    }

                    byte[] imageData = await dbQueue.GetImageAsync(imageId);

                    bool isSent = await SendImageToServerAsync(imageData);

                    if (isSent)
                    {
                        await dbQueue.DeleteImageAsync(imageId);
                    }
                    else
                    {
                        for (int retry = 1; retry <= maxRetries; retry++)
                        {
                            Console.WriteLine($"Retrying image {imageId}, attempt {retry}");
                            isSent = await SendImageToServerAsync(imageData);
                            if (isSent)
                            {
                                await dbQueue.DeleteImageAsync(imageId);
                                break;
                            }
                            await Task.Delay(1000); 
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        private async Task<bool> SendImageToServerAsync(byte[] imageData)
        {
            using HttpClient client = new HttpClient();

            try
            {
                ByteArrayContent content = new ByteArrayContent(imageData);
                HttpResponseMessage response = await client.PostAsync(serverUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Image sent successfully.");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Image sending failed with status code: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending image: {ex.Message}");
                return false;
            }
        }


    }







    public class HealthMonitor
    {
        private readonly DBQueue dbQueue;
        private readonly TimeSpan monitoringInterval;
        private readonly TimeSpan imageInsertionTimeout;
        private readonly TimeSpan lockDuration;

        private bool isMonitoring;
        private DateTime lastImageInsertionTime;

        public HealthMonitor(DBQueue dbQueue, TimeSpan monitoringInterval, TimeSpan imageInsertionTimeout, TimeSpan lockDuration)
        {
            this.dbQueue = dbQueue;
            this.monitoringInterval = monitoringInterval;
            this.imageInsertionTimeout = imageInsertionTimeout;
            this.lockDuration = lockDuration;

            this.isMonitoring = false;
            this.lastImageInsertionTime = DateTime.MinValue;
        }

        public void StartMonitoring()
        {
            isMonitoring = true;
            Task.Run(MonitorService);
        }

        public void StopMonitoring()
        {
            isMonitoring = false;
        }

        private async Task MonitorService()
        {
            while (isMonitoring)
            {
                try
                {
                    TimeSpan timeSinceLastInsertion = DateTime.UtcNow - lastImageInsertionTime;

                    if (timeSinceLastInsertion > imageInsertionTimeout)
                    {
                        Console.WriteLine($"No image inserted for {timeSinceLastInsertion}. Locking user account.");
                       

                        lastImageInsertionTime = DateTime.UtcNow; // Reset the timer
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error monitoring service: {ex.Message}");
                }

                await Task.Delay(monitoringInterval);
            }
        }
    }













    public class Configuration
    {
        private ClientWebSocket webSocket;
        private string serverUrl = "ws://localhost:8090"; 
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
