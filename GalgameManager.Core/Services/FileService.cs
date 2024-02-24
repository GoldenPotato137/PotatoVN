#nullable enable
using System.Collections.Concurrent;
using GalgameManager.Core.Contracts.Services;
using Newtonsoft.Json;

namespace GalgameManager.Core.Services;

public class FileService : IFileService
{
    private static readonly BlockingCollection<(string, string)> WritingQueue = new();

    public FileService()
    {
        Thread writer = new(WriteWorker)
        {
            IsBackground = true
        };
        writer.Start();
    }
    
    public T? Read<T>(string folderPath, string fileName, JsonSerializerSettings? settings = null)
    {
        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json, settings)!;
        }
        return default;
    }

    public string ReadWithoutJson(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    public void Save<T>(string folderPath, string fileName, T content, JsonSerializerSettings? settings = null)
    {
        if (folderPath == null)
            throw new Exception("folderPath is null");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);
        var filePath = Path.Combine(folderPath, fileName);
        WritingQueue.Add((filePath, JsonConvert.SerializeObject(content, settings)));
    }

    public void SaveNow<T>(string folderPath, string fileName, T content)
    {
        if (folderPath == null)
            throw new Exception("folderPath is null");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);
        var filePath = Path.Combine(folderPath, fileName);
        File.WriteAllText(filePath, JsonConvert.SerializeObject(content));
    }

    public void SaveWithoutJson(string folderPath, string fileName, string content)
    {
        if (folderPath == null)
            throw new Exception("folderPath is null");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);
        var filePath = Path.Combine(folderPath, fileName);
        WritingQueue.Add((filePath, content));
    }

    public void Delete(string folderPath, string fileName)
    {
        if (File.Exists(Path.Combine(folderPath, fileName)))
        {
            File.Delete(Path.Combine(folderPath, fileName));
        }
    }
    
    public async Task WaitForWriteFinishAsync()
    {
        while (WritingQueue.Count > 0)
        {
            await Task.Delay(100);
        }
    }

    private static void WriteWorker()
    {
        foreach (var (path, content) in WritingQueue.GetConsumingEnumerable())
        {
            File.WriteAllText(path, content);
        }
    }
}
