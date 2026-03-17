using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TaMi_Automatenclient
{
    public static class AppSettings
    {
        public static List<string> AutomatenNamen { get; private set; } = new List<string>();
        public static string IniPath { get; set; } = @"C:\ProgramData\SuE-Software\SuE-TaMi Client SQL\Automatenclient.ini";

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder retVal, int size, string filePath);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(string section, string key, string value, string filePath);

        public static string ReadIniValue(string section, string key, string defaultValue = "")
        {
            try
            {
                var sb = new StringBuilder(1024);
                GetPrivateProfileString(section, key, defaultValue ?? string.Empty, sb, sb.Capacity, IniPath);
                return sb.ToString();
            }
            catch { return defaultValue ?? string.Empty; }
        }

        public static void WriteIniValue(string section, string key, string value)
        {
            try
            {
                var dir = Path.GetDirectoryName(IniPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch { }

            try { WritePrivateProfileString(section, key, value ?? string.Empty, IniPath); } catch { }
        }

        // Liest ALLE "Name=" Einträge aus [Device]
        public static List<string> LoadAutomatenNamenFromIni()
        {
            var result = new List<string>();
            try
            {
                if (!File.Exists(IniPath)) return result;
                string section = null;
                foreach (var raw in File.ReadAllLines(IniPath))
                {
                    var line = (raw ?? "").Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    if (string.Equals(section, "Device", StringComparison.OrdinalIgnoreCase))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0)
                        {
                            var key = line.Substring(0, eq).Trim();
                            var value = line.Substring(eq + 1).Trim();
                            if (string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                                result.Add(value);
                        }
                    }
                }
            }
            catch { }

            AutomatenNamen = new List<string>(new HashSet<string>(result, StringComparer.OrdinalIgnoreCase));
            return AutomatenNamen;
        }
    }
}