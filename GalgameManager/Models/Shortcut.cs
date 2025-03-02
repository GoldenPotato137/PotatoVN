using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GalgameManager.Helpers.Steam;
public class Shortcut
{
    public Shortcut(int entryID, string appName, string exe, string startDir, string icon, string shortcutPath, string launchOptions, bool isHidden, bool allowDesktopConfig, bool allowOverlay, bool openVR, bool devkit, string devkitGameID, bool devkitOverrideAppID, string lastPlayTime, string flatpakAppID, List<string> tags)
    {
        EntryID = entryID;
        AppID = GenerateAppID(exe, appName);
        AppName = appName;
        Exe = exe;
        StartDir = startDir;
        Icon = icon;
        ShortcutPath = shortcutPath;
        LaunchOptions = launchOptions;
        IsHidden = isHidden;
        AllowDesktopConfig = allowDesktopConfig;
        AllowOverlay = allowOverlay;
        OpenVR = openVR;
        Devkit = devkit;
        DevkitGameID = devkitGameID;
        DevkitOverrideAppID = devkitOverrideAppID;
        LastPlayTime = lastPlayTime;
        FlatpakAppID = flatpakAppID;
        Tags = tags;
    }

    public Shortcut(int entryID, string appName, string exe, string startDir, string icon)
    {
        EntryID = entryID;
        AppID = GenerateAppID(exe, appName);
        AppName = appName;
        Exe = exe;
        StartDir = startDir;
        Icon = icon;
        ShortcutPath = "";
        LaunchOptions = "";
        DevkitGameID = "";
        FlatpakAppID = "";
        AllowDesktopConfig = true;
        AllowOverlay = true;
        LastPlayTime = "";
    }

    public Shortcut(string appName, string exe, string startDir, string icon)
    {
        EntryID = 0;
        AppID = GenerateAppID(exe, appName);
        AppName = appName;
        Exe = exe;
        StartDir = startDir;
        Icon = icon;
        AllowDesktopConfig = true;
        AllowOverlay = true;
        LastPlayTime = "";
    }

    public int EntryID { get; set; }
    public ulong AppID { get; set; }
    public string AppName { get; set; }
    public string Exe { get; set; }
    public string StartDir { get; set; }
    public string Icon { get; set; }
    public string ShortcutPath { get; set; }
    public string LaunchOptions { get; set; }
    public bool IsHidden { get; set; }
    public bool AllowDesktopConfig { get; set; }
    public bool AllowOverlay { get; set; }
    public bool OpenVR { get; set; }
    public bool Devkit { get; set; }
    public string DevkitGameID { get; set; }
    public bool DevkitOverrideAppID { get; set; }
    public string LastPlayTime { get; set; }
    public string FlatpakAppID { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    private static ulong GenerateAppID(string exe, string appName)
    {
        string input = $"\"{exe}\" {appName}";

        // 使用 CRC32 计算哈希值
        uint crc32Hash = ComputeCRC32(input);

        // 执行 OR 操作
        uint result1 = crc32Hash | 0x80000000;

        // 将结果左移 32 位
        ulong result2 = (ulong)result1 << 32;

        // 再次执行 OR 操作
        ulong finalResult = result2 | 0x02000000;
        Console.WriteLine(finalResult);
        return finalResult;
    }
    private static uint ComputeCRC32(string input)
    {
        using (var crc32 = new Crc32())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hash = crc32.ComputeHash(bytes);
            return BitConverter.ToUInt32(hash, 0);
        }
    }
}
