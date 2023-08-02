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

        string serverUrl = "http://localhost:5285";

        string filePath = "C:/Users/ankit/Desktop/password.txt"; 

       
        using (FileStream fileStream = File.OpenRead(filePath))
        {
           
            long fileSize = fileStream.Length;

           
            byte[] fileData = new byte[fileSize];
            await fileStream.ReadAsync(fileData, 0, (int)fileSize);

            
            var content = new ByteArrayContent(fileData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "file",
                FileName = Path.GetFileName(filePath)
            };

            var formData = new MultipartFormDataContent();
            formData.Add(content);

            
            var response = await httpClient.PostAsync(serverUrl, formData);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("File uploaded successfully!");
                
            }
            else
            {
                Console.WriteLine($"File upload failed. Status code: {response.StatusCode}");
               
            }
        }
    }
}
