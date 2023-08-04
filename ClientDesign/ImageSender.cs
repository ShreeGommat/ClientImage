using ClientApp;
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
}
