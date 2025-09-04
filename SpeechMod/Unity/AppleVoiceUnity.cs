using Kingmaker;
using Kingmaker.Blueprints;
using SpeechMod.Unity.Extensions;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UnityEngine;
using System.IO;
using System.Reflection;
using UnityModManagerNet;


namespace SpeechMod.Unity;

public class AppleVoiceUnity : MonoBehaviour
{
    private static AppleVoiceUnity m_TheVoice;
    public static UnityModManager.ModEntry ModEntry;
    private static string GenderVoice => Game.Instance?.DialogController?.CurrentSpeaker?.Gender == Gender.Female ? Main.FemaleVoice : Main.MaleVoice;
    private static int GenderRate => Game.Instance?.DialogController?.CurrentSpeaker?.Gender == Gender.Female ? Main.Settings!.FemaleRate : Main.Settings!.MaleRate;

    public static string GetScriptPath()
    {
        // Preferred: UMM gives you the mod's folder.
        string modDir = ModEntry?.Path;
        if (!string.IsNullOrEmpty(modDir))
            return Path.Combine(modDir, "sh", "sirisaywrapper.sh");

        // Fallback: directory of this mod assembly (works even if user moves the mod folder)
        var asmLocation = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(asmLocation))
        {
            var asmDir = Path.GetDirectoryName(asmLocation);
            if (!string.IsNullOrEmpty(asmDir))
                return Path.Combine(asmDir, "sh", "sirisaywrapper.sh");
        }

        // Last resort: current directory (unlikely correct, but avoids nulls)
        return Path.Combine(Directory.GetCurrentDirectory(), "sh", "sirisaywrapper.sh");
    }

    private static bool IsVoiceInitialized()
    {
        if (m_TheVoice != null)
            return true;

        Main.Logger.Critical("No voice initialized!");
        return false;
    }

    void Start()
    {
        if (m_TheVoice != null)
            Destroy(gameObject);
        else
            m_TheVoice = this;
    }
    public static string EscapeForBash(string input)
    {
        if (input == null) return string.Empty;

        var sb = new System.Text.StringBuilder(input.Length * 2);

        foreach (var c in input)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '$':
                    sb.Append("\\$");
                    break;
                case '`':
                    sb.Append("\\`");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
    public static void Speak(string text, float delay = 0f)
    {
        if (!IsVoiceInitialized())
            return;

        if (delay > 0f)
        {
            AppleVoiceUnity.m_TheVoice.ExecuteLater(delay, delegate
            {
                AppleVoiceUnity.Speak(text, 0f);
            });
            return;
        }
        AppleVoiceUnity.Stop();
        text = EscapeForBash(text);
        text = "Sirisay" + Main.NarratorVoice + " \"" + text + "\"";
        if (Main.Settings.LogVoicedLines)
            Main.Logger.Log($"Final Speak Command: {text}");
        Process.Start(GetScriptPath(), text);
    }

    public static void SpeakDialog(string text, float delay = 0f, string gender = "")
    {
        if (Main.Settings.LogVoicedLines && !string.IsNullOrWhiteSpace(text))
        {
            Main.Logger.Log($"SpeakerGender: {gender}");
            Main.Logger.Log($"SpeakDialog: {text}");
        }
        if (!IsVoiceInitialized())
                return;

        if (delay > 0f)
        {
            AppleVoiceUnity.m_TheVoice.ExecuteLater(delay, delegate
            {
                AppleVoiceUnity.SpeakDialog(text, 0f);
            });
            return;
        }
        string text2 = "";
        text = new Regex("<b><color[^>]+><link([^>]+)?>([^<>]*)</link></color></b>").Replace(text, "$2");
        text = text.Replace("\\n", "  ");
        text = text.Replace("\n", " ");
        text = text.Replace(";", "");
        while (text.IndexOf("<color=#3c2d0a>", StringComparison.InvariantCultureIgnoreCase) != -1)
        {
            int num = text.IndexOf("<color=#3c2d0a>", StringComparison.InvariantCultureIgnoreCase);
            if (num != 0)
            {
                string text3 = text.Substring(0, num);
                text = text.Substring(num);
                text2 = string.Format("{0}\"Sirisay{1}\" \"{2}\";", new object[]
                {
                    text2,
                    AppleVoiceUnity.GenderVoice,
                    text3.Replace("\"", "")
                });
            }
            else
            {
                num = text.IndexOf("</color>", StringComparison.InvariantCultureIgnoreCase);
                string text4 = text.Substring(0, num);
                text = text.Substring(num);
                text2 = string.Format("{0}\"Sirisay{1}\" \"{2}\";", new object[]
                {
                    text2,
                    Main.NarratorVoice,
                    text4.Replace("\"", "")
                });
            }
        }
        text = text.Replace("\"", "");
        if (!string.IsNullOrWhiteSpace(text) && text != "</color>")
        {
            text2 = string.Format("{0}\"Sirisay{1}\" \"{2}\";", new object[]
            {
                text2,
                AppleVoiceUnity.GenderVoice,
                text
            });
        }
        text2 = new Regex("<[^>]+>").Replace(text2, "");
        AppleVoiceUnity.KillAll();
        Process.Start(GetScriptPath(), text2);  
    }

    public static void Stop()
    {
        if (!IsVoiceInitialized())
            return;

        KillAll();
    }

    public static string[] GetAvailableVoices()
    {
        string str = "shortcuts list | grep '^Sirisay' | sed 's/^Sirisay//' | awk '{printf \\\"%s#Siri;\\\", $0}'";
        Process process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "-c \"" + str + "\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        process.Start();
        string text = process.StandardError.ReadToEnd();
        if (!string.IsNullOrWhiteSpace(text))
        {
            Main.Logger.Error(text);
        }
        string text2 = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        process.Dispose();
        if (string.IsNullOrWhiteSpace(text2))
        {
            return null;
        }
        return text2.Split(new string[]
        {
            ";"
        }, StringSplitOptions.RemoveEmptyEntries);
    }
    public static string GetStatusMessage()
    {
        if (!IsVoiceInitialized())
            return "Voice not initialized";

        return "Apple TTS is ready";
    }
    private static void KillAll()
    {
        Process.Start("/usr/bin/killall", "bash -kill");
        // no longer needed? 
        // Process.Start("/usr/bin/killall", "say -kill");
    }
}
