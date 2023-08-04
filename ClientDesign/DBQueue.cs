
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
using System.Data.SQLite;

namespace ClientApp
{
    public class DBQueue
    {
        private readonly string connectionString;
        private readonly string imagesFolder;

        public DBQueue(string connectionString, string imagesFolder)
        {
            this.connectionString = connectionString;
            this.imagesFolder = imagesFolder;

            
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

                return true; // Insertion successful
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
}
