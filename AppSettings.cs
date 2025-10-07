using System;
using System.Collections.Generic;
using System.IO;

namespace TaMi_Kassenclient
{
    public static class AppSettings
    {
        public static List<string> AutomatenNamen { get; private set; } = new List<string>();
        public static string IniPath { get; set; } = @"C:\ProgramData\SuE-Software\SuE-TaMi Client SQL\Kassenclient.ini";

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