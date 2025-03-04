using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GalgameManager.Models;
namespace GalgameManager.Helpers;
public class ShortcutHelper
{
    public class ShortcutPhrase
    {
        public static byte[] AppIdToHex(ulong longAppid)
        {
            byte[] bytes = BitConverter.GetBytes(longAppid);
            Array.Reverse(bytes);
            byte[] result = bytes.Take(4).Reverse().ToArray();
            return result;
        }
        public static ulong HexToAppId(byte[] hexBytes)
        {
            byte[] bytes = hexBytes.Reverse().ToArray();
            string reversedHex = BitConverter.ToString(bytes).Replace("-", "").ToLower();
            ulong longAppid = (ulong)Convert.ToInt32(reversedHex, 16) << 32 | 0x02000000;
            return longAppid;
        }
        public static string LastPlayTimeToHex(byte[] hexBytes)
        {
            byte[] bytes = hexBytes.Reverse().ToArray();
            Array.Reverse(bytes);
            string reversedHex = BitConverter.ToString(bytes).Replace("-", "").ToLower();
            long timestamp = Convert.ToInt64(reversedHex, 16);
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime dateTime = epoch.AddSeconds(timestamp);
            return dateTime.ToLocalTime().ToString();
        }


    }

    public class ShortcutWriter
    {
        public static void WriteToStream(Shortcut sc, Stream stream)
        {
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write((byte)0x00);
                WriteString(writer, sc.EntryID.ToString(), 1);
                writer.Write((byte)0x00);


                writer.Write((byte)0x02);
                writer.Write(Encoding.UTF8.GetBytes("appid"));
                writer.Write((byte)0x00);
                sc._base_appid=writer.BaseStream.Position;
                writer.Write(ShortcutPhrase.AppIdToHex(sc.AppID));

                WriteString(writer, "AppName", sc.AppName);
                WriteString(writer, "Exe", sc.Exe);
                WriteString(writer, "StartDir", sc.StartDir);

                WriteString(writer, "icon", sc.Icon);
                WriteString(writer, "ShortcutPath", sc.ShortcutPath);
                WriteString(writer, "LaunchOptions", sc.LaunchOptions);

                WriteBool(writer, "IsHidden", sc.IsHidden);
                WriteBool(writer, "AllowDesktopConfig", sc.AllowDesktopConfig);
                WriteBool(writer, "AllowOverlay", sc.AllowOverlay);
                WriteBool(writer, "OpenVR", sc.OpenVR);
                WriteBool(writer, "Devkit", sc.Devkit);

                WriteString(writer, "DevkitGameID", sc.DevkitGameID);


                WriteBool(writer, "DevkitOverrideAppID", sc.DevkitOverrideAppID);

                writer.Write((byte)0x02);
                writer.Write(Encoding.UTF8.GetBytes("LastPlayTime"));
                writer.Write((byte)0x00);
                sc._base_LastPlayTime = writer.BaseStream.Position;
                if (sc.LastPlayTime == "")
                {
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                }
                else
                {

                    writer.Write(Encoding.UTF8.GetBytes(sc.LastPlayTime));
                }
                WriteString(writer, "FlatpakAppID", sc.FlatpakAppID);

                writer.Write(Encoding.UTF8.GetBytes("tags"));
                writer.Write((byte)0x00);
                foreach (var tag in sc.Tags)
                {
                    writer.Write((byte)0x01);
                    writer.Write(Encoding.UTF8.GetBytes(tag));
                    writer.Write((byte)0x00);
                }

                writer.Write((byte)0x08);
                writer.Write((byte)0x08);
                writer.Write((byte)0x08);
                writer.Write((byte)0x08);
            }
        }
        private static void WriteString(BinaryWriter writer, string value, int byteCount)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > byteCount)
            {
                throw new ArgumentException($"The value '{value}' exceeds the specified byte count of {byteCount}.");
            }

            writer.Write(bytes);

            // 如果字节数不足，填充空字节
            for (int i = bytes.Length; i < byteCount; i++)
            {
                writer.Write((byte)0x00);
            }
        }
        private static void WriteString(BinaryWriter writer, string key, string value)
        {
            writer.Write((byte)0x01);
            writer.Write(Encoding.UTF8.GetBytes(key));
            writer.Write((byte)0x00);
            if (value == "")
            {
                writer.Write((byte)0x00);
                return;
            }
            byte[] op = Encoding.UTF8.GetBytes(value);
            writer.Write(op);
            writer.Write((byte)0x00);
        }

        private static void WriteBool(BinaryWriter writer, string key, bool value)
        {
            writer.Write((byte)0x02);
            writer.Write(Encoding.UTF8.GetBytes(key));
            writer.Write((byte)0x00);
            writer.Write(value ? (byte)0x01 : (byte)0x00);
            writer.Write((byte)0x00);
            writer.Write((byte)0x00);
            writer.Write((byte)0x00);
        }
        public static ulong Add_no_steam_game(Shortcut sc, string path_to_shortcuts)
        {

            using (var fileStream = new FileStream(path_to_shortcuts, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fileStream.Seek(-2, SeekOrigin.End);
                WriteToStream(sc, fileStream);
            }
            return sc.AppID;
        }
        
    }

    public class ShortcutReader
    {
        public static List<Shortcut> ReadShortcuts(string pathToShortcuts)
        {
            List<Shortcut> shortcuts = new List<Shortcut>();

            using (var fileStream = new FileStream(pathToShortcuts, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fileStream, Encoding.UTF8))
            {
                // 读取文件开头的固定标识
                byte[] header = reader.ReadBytes(11);
                string headerString = Encoding.UTF8.GetString(header);
                if (headerString != "\x00shortcuts\x00")
                {
                    throw new InvalidDataException("文件开头不匹配");
                }

                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    // 检查是否到达文件结束标志
                    if (IsEndOfFile(reader))
                    {
                        break;
                    }

                    // 读取每个Shortcut对象
                    Shortcut shortcut = ReadShortcut(reader);
                    shortcuts.Add(shortcut);
                }
            }

            return shortcuts;
        }

        private static Shortcut ReadShortcut(BinaryReader reader)
        {
            int entryID = 0;
            string appName = "";
            string exe = "";
            string startDir = "";
            string icon = "";
            string shortcutPath = "";
            string launchOptions = "";
            bool isHidden = false;
            bool allowDesktopConfig = true;
            bool allowOverlay = true;
            bool openVR = false;
            bool devkit = false;
            string devkitGameID = "";
            bool devkitOverrideAppID = false;
            string lastPlayTime = "";
            string flatpakAppID = "";
            List<string> tags = new List<string>();
            int flag = 0;
            long base_appid=0;
            long base_LastPlayTime=0;
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte type = reader.ReadByte();
                if (type == 0x08)
                {
                    // 检查是否是Shortcut对象的结束标志
                    if (reader.ReadByte() == 0x08)
                    {
                        break;
                    }
                    else
                    {
                        reader.BaseStream.Seek(-1, SeekOrigin.Current);
                    }
                }
                if (type == 0x00 || type == 0x01 || type == 0x02)
                {
                    continue;
                }
                if (flag == 0)
                {
                    byte[] types = { type };
                    entryID = int.Parse(Encoding.UTF8.GetString(types));
                    flag = 1;
                    continue;
                }
                reader.BaseStream.Seek(-1, SeekOrigin.Current);
                string key = Read_String(reader);
                switch (key)
                {
                    case "appid":
                        base_appid = reader.BaseStream.Position;
                        ulong appID = ShortcutPhrase.HexToAppId(reader.ReadBytes(4));
                        break;
                    case "AppName":
                        appName = Read_String(reader);
                        break;
                    case "Exe":
                        exe = Read_String(reader);
                        break;
                    case "StartDir":
                        startDir = Read_String(reader);
                        break;
                    case "icon":
                        icon = Read_String(reader);
                        break;
                    case "ShortcutPath":
                        shortcutPath = Read_String(reader);
                        break;
                    case "LaunchOptions":
                        launchOptions = Read_String(reader);
                        break;
                    case "IsHidden":
                        isHidden = ReadBool(reader);
                        break;
                    case "AllowDesktopConfig":
                        allowDesktopConfig = ReadBool(reader);
                        break;
                    case "AllowOverlay":
                        allowOverlay = ReadBool(reader);
                        break;
                    case "OpenVR":
                        openVR = ReadBool(reader);
                        break;
                    case "Devkit":
                        devkit = ReadBool(reader);
                        break;
                    case "DevkitGameID":
                        devkitGameID = Read_String(reader);
                        break;
                    case "DevkitOverrideAppID":
                        devkitOverrideAppID = ReadBool(reader);
                        break;
                    case "LastPlayTime":
                        base_LastPlayTime =reader.BaseStream.Position;
                        lastPlayTime = Read_LastPlayTime(reader);
                        break;
                    case "FlatpakAppID":
                        flatpakAppID = Read_String(reader);
                        break;
                    case "tags":
                        tags = ReadTags(reader);
                        break;
                    default:
                        throw new InvalidDataException($"未知的键: {key}");
                }
            }

            return new Shortcut(entryID, appName, exe, startDir, icon, shortcutPath, launchOptions, isHidden, allowDesktopConfig, allowOverlay, openVR, devkit, devkitGameID, devkitOverrideAppID, lastPlayTime, flatpakAppID, tags,base_appid,base_LastPlayTime);
        }

        private static string Read_String(BinaryReader reader)
        {

            List<byte> bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0x00)
            {
                bytes.Add(b);
            }
            if (bytes.Count == 0)
            {
                return "";
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
        private static string Read_LastPlayTime(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            Array.Reverse(bytes);
            string reversedHex = BitConverter.ToString(bytes).Replace("-", "").ToLower();
            long timestamp = Convert.ToInt64(reversedHex, 16);
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime dateTime = epoch.AddSeconds(timestamp);
            return dateTime.ToLocalTime().ToString();
        }
        private static bool ReadBool(BinaryReader reader)
        {
            return reader.ReadByte() == 0x01;
        }

        private static List<string> ReadTags(BinaryReader reader)
        {
            List<string> tags = new List<string>();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte type = reader.ReadByte();
                if (type == 0x08)
                {
                    // 检查是否是tags的结束标志
                    if (reader.ReadByte() == 0x08)
                    {
                        break;
                    }
                    else
                    {
                        reader.BaseStream.Seek(-1, SeekOrigin.Current);
                    }
                }
                string tag = Read_String(reader);
                tags.Add(tag);
            }
            reader.BaseStream.Seek(-2, SeekOrigin.Current);
            return tags;
        }

        private static bool IsEndOfFile(BinaryReader reader)
        {
            if (reader.BaseStream.Position + 2 > reader.BaseStream.Length)
            {
                return false;
            }

            byte[] endBytes = reader.ReadBytes(2);
            if (endBytes[0] == 0x08 && endBytes[1] == 0x08)
            {
                return true;
            }

            reader.BaseStream.Seek(-2, SeekOrigin.Current);
            return false;
        }
    }
}

