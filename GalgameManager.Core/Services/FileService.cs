using System.Text;
using GalgameManager.Core.Contracts.Services;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace GalgameManager.Core.Services;

public class FileService : IFileService
{
    public T Read<T>(string folderPath, string fileName, bool useJsonSerializer = false)
    {
        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return useJsonSerializer ? JsonSerializer.Deserialize<T>(json) : JsonConvert.DeserializeObject<T>(json);
        }

        return default;
    }

    public async Task Save<T>(string folderPath, string fileName, T content, bool useJsonSerializer = false)
    {
        if (folderPath == null)
        {
            throw new Exception("folderPath is null");
        }

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var fileContent = useJsonSerializer ? JsonSerializer.Serialize(content) : JsonConvert.SerializeObject(content);
        var filePath = Path.Combine(folderPath, fileName);

        const int maxRetries = 3;
        const int delayOnRetry = 1000;

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 4096, true);
                await using var streamWriter = new StreamWriter(fileStream, Encoding.UTF8);
                await streamWriter.WriteAsync(fileContent);
                break;
            }
            catch (IOException)
            {
                if (i < maxRetries - 1)
                {
                    await Task.Delay(delayOnRetry);
                }
                else
                {
                    throw;
                }
            }
        }
    }


    public void Delete(string folderPath, string fileName)
    {
        if (fileName != null && File.Exists(Path.Combine(folderPath, fileName)))
        {
            File.Delete(Path.Combine(folderPath, fileName));
        }
    }
}
