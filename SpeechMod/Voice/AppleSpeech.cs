using Kingmaker;
using SpeechMod.Unity;
using System;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kingmaker.Blueprints;


namespace SpeechMod.Voice;

public class AppleSpeech : ISpeech
{
    private static string SpeakBegin => "";
    private static string SpeakEnd => "";
    
    private static string SpeakerVoice => Game.Instance?.DialogController?.CurrentSpeaker?.Gender == Gender.Female ? Main.FemaleVoice : Main.MaleVoice;
    private static string SpeakerGender =>
    Game.Instance?.DialogController?.CurrentSpeaker?.Gender switch
    {
        Gender.Female => "Female",
        Gender.Male => "Male",
        _ => "Narrator"
    };
    private static string NarratorVoice => $"<voice required=\"Name={Main.NarratorVoice}\">";
    private static string NarratorPitch => $"<pitch absmiddle=\"{Main.Settings?.NarratorPitch}\"/>";
    private static string NarratorRate => $"<rate absspeed=\"{Main.Settings?.NarratorRate}\"/>";
    private static string NarratorVolume => $"<volume level=\"{Main.Settings?.NarratorVolume}\"/>";

    private static string FemaleVoice => $"<voice required=\"Name={Main.FemaleVoice}\">";
    private static string FemaleVolume => $"<volume level=\"{Main.Settings?.FemaleVolume}\"/>";
    private static string FemalePitch => $"<pitch absmiddle=\"{Main.Settings?.FemalePitch}\"/>";
    private static string FemaleRate => $"<rate absspeed=\"{Main.Settings?.FemaleRate}\"/>";

    private static string MaleVoice => $"<voice required=\"Name={Main.MaleVoice}\">";
    private static string MaleVolume => $"<volume level=\"{Main.Settings?.MaleVolume}\"/>";
    private static string MalePitch => $"<pitch absmiddle=\"{Main.Settings?.MalePitch}\"/>";
    private static string MaleRate => $"<rate absspeed=\"{Main.Settings?.MaleRate}\"/>";

    public string CombinedNarratorVoiceStart => $"{NarratorVoice}";
    public string CombinedFemaleVoiceStart => $"{FemaleVoice}";
    public string CombinedMaleVoiceStart => $"{MaleVoice}";

    public virtual string CombinedDialogVoiceStart
    {
        get
        {
            if (Game.Instance?.DialogController?.CurrentSpeaker == null)
                return CombinedNarratorVoiceStart;

            return Game.Instance.DialogController.CurrentSpeaker.Gender switch
            {
                Gender.Female => CombinedFemaleVoiceStart,
                Gender.Male => CombinedMaleVoiceStart,
                _ => CombinedNarratorVoiceStart
            };
        }
    }

    public static int Length(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var arr = new[] { "—", "-", "\"" };

        return arr.Aggregate(text, (current, t) => current.Replace(t, "")).Length;
    }

    private string FormatGenderSpecificVoices(string text)
    {
        return text;
    }

    private void SpeakInternal(string text, float delay = 0f)
    {
        text = SpeakBegin + text + SpeakEnd;
        AppleVoiceUnity.Speak(text, delay);
    }

    public bool IsSpeaking()
    {
        return false;
    }

    public void SpeakPreview(string text, VoiceType voiceType)
    {
        if (string.IsNullOrEmpty(text))
        {
            Main.Logger?.Warning("No text to speak!");
            return;
        }

        text = text.PrepareText();
        text = new Regex("<[^>]+>").Replace(text, "");

        SpeakAs(text, voiceType);
    }

    public string PrepareSpeechText(string text)
    {
        text = new Regex("<[^>]+>").Replace(text, "");
        text = text.PrepareText();
        return text;
    }

    public string PrepareDialogText(string text)
    {
        text = text.PrepareText();
        text = Regex.Replace(text, @"<link[^>]*>(.*?)</link>", "$1", RegexOptions.Singleline);
        text = Regex.Replace(text, @"</?[^>]+>", "");

		// text = FormatGenderSpecificVoices(text);
        return text;
    }
    public static List<string[]> BuildSpeakList(string input)
    {
        string narratorVoice = Main.NarratorVoice;
        string defaultVoice  = SpeakerVoice;

        var res = new List<string[]>();
        if (string.IsNullOrEmpty(input)) return res;

        // <i ...>...</i> (beliebige Attribute, case-insensitive, über Zeilen)
        var iTag = new Regex(@"<color\s*=\s*#616060>(.*?)</color>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        int pos = 0;
        foreach (Match m in iTag.Matches(input))
        {
            // Text vor dem <i>-Block → defaultVoice
            if (m.Index > pos)
            {
                string outside = input.Substring(pos, m.Index - pos);
                if (outside.Length > 0)
                    res.Add(new[] { outside, defaultVoice });
            }

            // Inhalt des <i>-Blocks (ohne die Tags) → narratorVoice
            string inside = m.Groups[1].Value;
            if (inside.Length > 0)
                res.Add(new[] { inside, narratorVoice });

            pos = m.Index + m.Length;
        }

        // Rest nach letztem <i>-Block → defaultVoice
        if (pos < input.Length)
        {
            string tail = input.Substring(pos);
            if (tail.Length > 0)
                res.Add(new[] { tail, defaultVoice });
        }

        return res;
    }
    public void SpeakDialog(string text, float delay = 0f)
    {
        // fire-and-forget: startet die Async-Variante ohne den Main Thread zu blockieren
        _ = SpeakDialogAsync(text, delay);
    }
    private async Task SpeakDialogAsync(string text, float delay = 0f)
    {   
        if (Main.Settings.LogVoicedLines)
        {
            Main.Logger.Log($"SpeakerGender: {SpeakerGender}");
            string text_safe = text.Replace("<", "&lt;").Replace(">", "&gt;");
            Main.Logger.Log($"SpeakDialog: {text_safe}");
        }
        
        text = Regex.Replace(text, @"<link[^>]*>(.*?)</link>", "$1",
    RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (string.IsNullOrEmpty(text))
        {
            Main.Logger?.Warning("No text to speak!");
            return;
        }
        if (!Main.Settings.UseGenderSpecificVoices)
        {
            text = PrepareDialogText(text);
            Speak(text, delay);
            return;
        }
        var list = BuildSpeakList(text);
        foreach (var entry in list)
        {
            string text2 = PrepareDialogText(entry[0]);
            text2 = AppleVoiceUnity.EscapeForBash(text2);
            string str = Unity.AppleVoiceUnity.GetScriptPath().Replace(" ", "\\ ") + " " + entry[1] + " " + "\\\"" + text2 + "\\\"";

            await Task.Run(() =>
            {
                using (var process = new Process())
                {
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
                    if (Main.Settings.LogVoicedLines)
                        Main.Logger.Log($"Final Speak Command: {process.StartInfo.FileName} {process.StartInfo.Arguments}");
                    process.WaitForExit(); // blockiert nur in diesem Hintergrund-Thread
                }
            });
        }
    }

    public void SpeakAs(string text, VoiceType voiceType, float delay = 0f)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			Main.Logger?.Warning("No text to speak!");
			return;
		}

		if (!Main.Settings!.UseGenderSpecificVoices)
		{
			Speak(text, delay);
			return;
		}

		// strip embedded quotes from the spoken text to avoid breaking the format
		var sanitized = text.Replace("\"", "");

		// Build "voice" "text"
		string formatted;
		switch (voiceType)
		{
			case VoiceType.Narrator:
				formatted = $"\"{Main.NarratorVoice}\" \"{sanitized}\"";
				break;

			case VoiceType.Female:
				formatted = $"\"{Main.FemaleVoice}\" \"{sanitized}\"";
				break;

			case VoiceType.Male:
				formatted = $"\"{Main.MaleVoice}\" \"{sanitized}\"";
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(voiceType), voiceType, null);
		}
		Process.Start(Unity.AppleVoiceUnity.GetScriptPath(), formatted);
	}
    public void Speak(string text, float delay = 0f)
    {
        if (string.IsNullOrEmpty(text))
        {
            Main.Logger?.Warning("No text to speak!");
            return;
        }

        text = PrepareSpeechText(text);

        SpeakInternal(text, delay);
    }

    public void Stop()
    {
        AppleVoiceUnity.Stop();
    }

    public string[] GetAvailableVoices()
    {
        return AppleVoiceUnity.GetAvailableVoices();
    }

    public string GetStatusMessage()
    {
        return AppleVoiceUnity.GetStatusMessage();
    }
}