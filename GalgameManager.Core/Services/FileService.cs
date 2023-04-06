using System.Text;

using GalgameManager.Core.Contracts.Services;

using Newtonsoft.Json;

namespace GalgameManager.Core.Services;

public class FileService : IFileService
{
    public T Read<T>(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }

        return default;
    }

    public async Task Save<T>(string folderPath, string fileName, T content)
    {
        if (folderPath == null)
        {
            throw new Exception("folderPath is null");
        }

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var fileContent = JsonConvert.SerializeObject(content);
        var filePath = Path.Combine(folderPath, fileName);

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await using var streamWriter = new StreamWriter(fileStream, Encoding.UTF8);
        await streamWriter.WriteAsync(fileContent);
    }

    public void Delete(string folderPath, string fileName)
    {
        if (fileName != null && File.Exists(Path.Combine(folderPath, fileName)))
        {
            File.Delete(Path.Combine(folderPath, fileName));
        }
    }
}
