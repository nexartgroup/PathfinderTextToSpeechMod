using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityModManagerNet;

namespace SpeechMod.Voice;

public static class PhoneticDictionary
{
    public static UnityModManager.ModEntry ModEntry;
    private static Dictionary<string, string> s_PhoneticDictionary = new();

    private static string SpaceOutDate(string text)
    {
        var pattern = @"([0-9]{2})\/([0-9]{2})\/([0-9]{4})";
        return Regex.Replace(text, pattern, "$1 / $2 / $3");
    }
    public static string GetModFolderPath()
    {
        // Preferred: UMM gives you the mod's folder.
        string modDir = ModEntry?.Path;
        if (!string.IsNullOrEmpty(modDir))
            return Path.Combine(modDir);

        // Fallback: directory of this mod assembly (works even if user moves the mod folder)
        var asmLocation = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(asmLocation))
        {
            var asmDir = Path.GetDirectoryName(asmLocation);
            if (!string.IsNullOrEmpty(asmDir))
                return Path.Combine(asmDir);
        }

        // Last resort: current directory (unlikely correct, but avoids nulls)
        return Path.Combine(Directory.GetCurrentDirectory());
    }
    public static string PrepareText(this string text)
    {
        if (s_PhoneticDictionary == null || !s_PhoneticDictionary.Any())
            LoadDictionary();

        //text = text.ToLower();
        text = text.Replace("\"", "");
        text = text.Replace("\r\n", ". ");
        text = text.Replace("\n", ". ");
        text = text.Replace("\r", ". ");
        text = text.Trim();
        text = SpaceOutDate(text);

        // Regex enabled dictionary
        return s_PhoneticDictionary?.Aggregate(text, (current, entry) => Regex.Replace(current, entry.Key, entry.Value));
    }

    public static void LoadDictionary()
    {
        //Main.Logger?.Log("Loading phonetic dictionary...");
        try
        {
            string modDir = GetModFolderPath();
            var file = Path.Combine(modDir, "PhoneticDictionary.json");
            //var file = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? throw new FileNotFoundException("Path to Pathfinder could not be found!"), @"Mods", @"SpeechMod", @"PhoneticDictionary.json");
            var json = File.ReadAllText(file, Encoding.UTF8);
            s_PhoneticDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }
        catch (Exception ex)
        {
            Main.Logger?.LogException(ex);
        }

#if DEBUG
        foreach (var entry in s_PhoneticDictionary)
        {
            Main.Logger?.Log($"{entry.Key}={entry.Value}");
        }
#endif
    }
}