using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GalgameManager.Models;
namespace GalgameManager.Helpers.Steam;
public class ShortPhrase
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

}
public class ShortcutWriter {
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
            writer.Write(ShortPhrase.AppIdToHex(sc.AppID));

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
    public static void Add_no_steam_game(Shortcut sc, string path_to_shortcuts)
    {

        using (var fileStream = new FileStream(path_to_shortcuts, FileMode.OpenOrCreate, FileAccess.ReadWrite))
        {
            fileStream.Seek(-2, SeekOrigin.End);
            WriteToStream(sc, fileStream);
        }
    }
    public static Shortcut Galgame_to_shortcut(Galgame game)
    {
        if (game.IsLocalGame && game.ExePath != null)
        {
            Shortcut sc = new Shortcut(game.Name, "\"" + game.ExePath + "\"", game.LocalPath + "\\", "");
            return sc;
        }
        return null;
    }
}
public class ShortcutReader { }
