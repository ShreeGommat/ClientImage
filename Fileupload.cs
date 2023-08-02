using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

public class FileUploadExample
{
    public static async Task Main(string[] args)
    {
        using var httpClient = new HttpClient();

        string serverUrl = "http://localhost:5285"; // Replace with the actual server URL

        string filePath = "C:/Users/ankit/Desktop/password.txt"; // Replace this with the actual file path

        // Open a file stream to read the file data
        using (FileStream fileStream = File.OpenRead(filePath))
        {
            // Determine the file size
            long fileSize = fileStream.Length;

            // Read the file data into a byte array
            byte[] fileData = new byte[fileSize];
            await fileStream.ReadAsync(fileData, 0, (int)fileSize);

            // Create the content to be sent in the HTTP request
            var content = new ByteArrayContent(fileData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "file",
                FileName = Path.GetFileName(filePath)
            };

            var formData = new MultipartFormDataContent();
            formData.Add(content);

            // Send the POST request to the server
            var response = await httpClient.PostAsync(serverUrl, formData);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("File uploaded successfully!");
                // Handle the server's response if needed
            }
            else
            {
                Console.WriteLine($"File upload failed. Status code: {response.StatusCode}");
                // Handle the error (e.g., non-successful status code)
            }
        }
    }
}
