using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NfcAime.Dll;

internal static class Config
{
    internal const string IOSection = "aimeio";
    private const string ConfigFileName = @".\segatools.ini";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetPrivateProfileString(
        string lpAppName,
        string lpKeyName,
        string lpDefault,
        StringBuilder lpReturnedString,
        uint nSize,
        string lpFileName);

    static Config()
    {
        IDmMode = Convert.ToInt32(ReadKey(IOSection, "IDmMode", 1024, "1"));
        ReaderCOM = ReadKey(IOSection, "ReaderCOM" , 1024, "COM3");
        ReaderBaud = Convert.ToInt32(ReadKey(IOSection, "Baud" , 1024, "115200"));
    }

    public static int IDmMode { get; }
    public static string ReaderCOM { get; }
    public static int ReaderBaud { get; }

    internal static string ReadKey(string section, string key, uint maxLength, string @default = null)
    {
        var sb = new StringBuilder((int)maxLength);
        GetPrivateProfileString(section, key, @default ?? string.Empty, sb, maxLength, ConfigFileName);
        return sb.ToString();
    }
}
