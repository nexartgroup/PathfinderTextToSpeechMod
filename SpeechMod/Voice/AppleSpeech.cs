using Kingmaker;
using SpeechMod.Unity;
using System;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Kingmaker.Blueprints;
using Kingmaker.Sound;
using Kingmaker.Settings;
using System.Collections.Concurrent;
using UnityEngine;


namespace SpeechMod.Voice;

public class AppleSpeech : ISpeech
{
    public sealed class WwiseDuckScope : IDisposable
    {
        private float _prev_voice;
        private float _prev_dialog;
        private float _prev_music;
        private bool _done;

        public WwiseDuckScope(float target01 = 0.2f)
        {
            if (!Main.Settings.DuckOnPlay)
            {
                _done = true;
                return; // no ducking
            }

            try
            {
                // --- 1) Werte direkt aus den Spiel-Einstellungen holen (0..100)
                var snd = SettingsRoot.Sound;

                _prev_voice  = snd?.VolumeVoices?.GetTempValue()   ?? 100f;
                _prev_dialog = snd?.VolumeDialogue?.GetTempValue() ?? 100f;

                // Music beachtet MuteMusic: Controller setzt in dem Fall 0 in MusicLevel
                bool musicMuted = snd?.MuteMusic?.GetTempValue() ?? false;
                _prev_music = musicMuted ? 0f : (snd?.VolumeMusic?.GetTempValue() ?? 100f);
            }
            catch
            {
                // Fallback, falls Settings unerwartet nicht verfügbar sind
                _prev_voice = _prev_dialog = _prev_music = 100f;
            }

            // --- 2) Ducking-Ziel (0..1) invertieren -> Projekt-Skala (0..100)
            float target = (1f - Mathf.Clamp01(target01)) * 100f;

            // Prozent der Originalwerte erhalten
            float factor = Mathf.Clamp01(target01); // 0 = 100% Dämpfung, 1 = keine Dämpfung

            float targetVoice   = _prev_voice  * factor;
            float targetDialog  = _prev_dialog * factor;
            float targetMusic   = _prev_music  * factor;

            /* if (Main.Settings.LogVoicedLines)
                Main.Logger.Log($"WwiseDuckScope(Settings): " +
                                $"VoiceLevel {_prev_voice} -> {targetVoice}, " +
                                $"DialogueLevel {_prev_dialog} -> {targetDialog}, " +
                                $"MusicLevel {_prev_music} -> {targetMusic}");
 */
            // --- 3) Duck setzen
            AkSoundEngine.SetRTPCValue("VoiceLevel",    targetVoice, null, 0);
            AkSoundEngine.SetRTPCValue("DialogueLevel", targetDialog, null, 0);
            AkSoundEngine.SetRTPCValue("MusicLevel",    targetMusic, null, 0);
        }

        public void Dispose()
        {
            if (_done) return;
            _done = true;

            // Zurück zu den Spiel-Einstellungen (inkl. Mute-Logik, die wir oben schon berücksichtigt haben)
            AkSoundEngine.SetRTPCValue("VoiceLevel",    _prev_voice,  null, 0);
            AkSoundEngine.SetRTPCValue("DialogueLevel", _prev_dialog, null, 0);
            AkSoundEngine.SetRTPCValue("MusicLevel",    _prev_music,  null, 0);
        }
    }
    public readonly struct SlncSegment
    {
        public readonly string Text;   // der zu sprechende Text (kann leer sein)
        public readonly int? PauseMs;  // Pause NACH diesem Segment (null = keine Pause)

        public SlncSegment(string text, int? pauseMs)
        {
            Text = text;
            PauseMs = pauseMs;
        }
    }
    static IEnumerable<SlncSegment> ParseSlnc(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            yield return new SlncSegment(string.Empty, null);
            yield break;
        }

        // [[slnc 1000]]  -> erfasst 1000
        var rx = new Regex(@"\[\[\s*slnc\s+(\d+)\s*\]\]", RegexOptions.IgnoreCase);
        int last = 0;

        foreach (Match m in rx.Matches(input))
        {
            // Text VOR dem Tag
            string before = input.Substring(last, m.Index - last);
            int pause = int.Parse(m.Groups[1].Value);

            // Pause gilt NACH 'before'
            yield return new SlncSegment(before, pause);

            // weiter hinter dem Tag
            last = m.Index + m.Length;
        }

        // Rest hinter dem letzten Tag (ohne folgende Pause)
        if (last <= input.Length)
        {
            string tail = input.Substring(last);
            yield return new SlncSegment(tail, null);
        }
    }
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

    private async Task SpeakInternal(string text, float delay = 0f)
    {
        // vorbereiten: [[slnc N]] bleibt im Text enthalten
        string prepared = PrepareDialogText(text);
        var segments = ParseSlnc(prepared);
        using (new WwiseDuckScope(Main.Settings.DuckOnPlayVolume)) // 20% Masterlautstärke
        {
            foreach (var seg in segments)
            {
                // 1) sprechen (nur wenn seg.Text nicht leer ist)
                if (!string.IsNullOrWhiteSpace(seg.Text))
                {
                    // Falls ein Startdelay gesetzt wurde, nur beim ersten Segment anwenden
                    if (delay > 0f)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delay));
                        delay = 0f; // nur einmal anwenden
                    }

                    string text2 = AppleVoiceUnity.EscapeForBash(seg.Text);
                    string script = Unity.AppleVoiceUnity.GetScriptPath().Replace(" ", "\\ ");
                    string cmd = $"{script} Sirisay{Main.NarratorVoice} \\\"{text2}\\\"";

                    await Task.Run(() =>
                    {
                        using (var process = new Process())
                        {
                            process.StartInfo = new ProcessStartInfo
                            {
                                FileName = "/bin/bash",
                                Arguments = "-c \"" + cmd + "\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };
                            process.Start();
                            if (Main.Settings.LogVoicedLines)
                                Main.Logger.Log($"Final Speak Command: {process.StartInfo.FileName} {process.StartInfo.Arguments}");
                            process.WaitForExit();
                        }
                    });
                }

                // 2) Pause danach (falls von [[slnc N]] vorgegeben)
                if (seg.PauseMs.HasValue && seg.PauseMs.Value > 0)
                {
                    await Task.Delay(seg.PauseMs.Value);
                }
                else
                {
                    await Task.Delay(100); // kurze Pause, damit nicht alles ineinanderfließt - Siribugfix
                }
            }
        }
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
        string defaultVoice = SpeakerVoice;

        var res = new List<string[]>();
        if (string.IsNullOrEmpty(input)) return res;

        // <i ...>...</i> (beliebige Attribute, case-insensitive, über Zeilen)
        var iTag = new Regex(@"<color=#616060>((?:(?>[^<]+)|<(?!/?color\b)|(?<open><color(?:\s*=\s*#[0-9A-Fa-f]{3,8})?\s*>)|(?<-open></color>))*(?(open)(?!)))</color>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

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
                    // >>> Spiel drosseln bis ALLES (inkl. Pausen) vorbei ist
        using (new WwiseDuckScope(Main.Settings.DuckOnPlayVolume)) // 20% Masterlautstärke
        {
        foreach (var entry in list)
        {
            // entry[0] = Text, entry[1] = Optionen/Flags für Sirisay (wie gehabt)
            string prepared = PrepareDialogText(entry[0]); // ← hier steht bereits [[slnc N]]
            var segments = ParseSlnc(prepared);

            foreach (var seg in segments)
            {
                // 1) sprechen (nur wenn seg.Text nicht leer/whitespace)
                if (!string.IsNullOrWhiteSpace(seg.Text))
                {
                    string text2 = AppleVoiceUnity.EscapeForBash(seg.Text);
                    string script = Unity.AppleVoiceUnity.GetScriptPath().Replace(" ", "\\ ");
                    string cmd = $"{script} Sirisay{entry[1]} \\\"{text2}\\\"";

                    await Task.Run(() =>
                    {
                        using (var process = new Process())
                        {
                            process.StartInfo = new ProcessStartInfo
                            {
                                FileName = "/bin/bash",
                                Arguments = "-c \"" + cmd + "\"",
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

                    // 2) Pause danach
                    if (seg.PauseMs.HasValue && seg.PauseMs.Value > 0)
                    {
                        await Task.Delay(seg.PauseMs.Value); // nicht blockierend
                    }
                    else
                    {
                        await Task.Delay(100); // kurze Pause, damit nicht alles ineinanderfließt - Siribugfix
                    }
            }

        }
        }
    }

    public void SpeakAs(string text, VoiceType voiceType, float delay = 0f)
    {
        // Wrapper, damit bestehende Aufrufer mit der alten Signatur weiterhin funktionieren.
        // Fire-and-forget ist hier konsistent mit dem bisherigen Verhalten von Process.Start.
        _ = SpeakAsAsync(text, voiceType, delay);
    }

    public async Task SpeakAsAsync(string text, VoiceType voiceType, float delay = 0f)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Main.Logger?.Warning("No text to speak!");
            return;
        }

        // Falls genderspezifische Stimmen nicht genutzt werden sollen, altes Verhalten beibehalten.
        if (!Main.Settings!.UseGenderSpecificVoices)
        {
            // Annahme: Die bestehende Speak-Methode kümmert sich selbst um evtl. delay.
            Speak(text, delay);
            return;
        }

        // Optionaler Initial-Delay (wie bisher über Parameter steuerbar)
        if (delay > 0f)
        {
            var ms = (int)(delay * 1000f);
            await Task.Delay(ms);
        }

        // 1) Text vorbereiten (hier stehen bereits [[slnc N]] Marker drin)
        string prepared = PrepareDialogText(text);

        // 2) In Segmente + Pausen aufteilen
        var segments = ParseSlnc(prepared);

        // 3) Voice-Auswahl bestimmen
        string voiceCmd = voiceType switch
        {
            VoiceType.Narrator => $"Sirisay{Main.NarratorVoice}",
            VoiceType.Female => $"Sirisay{Main.FemaleVoice}",
            VoiceType.Male => $"Sirisay{Main.MaleVoice}",
            _ => throw new ArgumentOutOfRangeException(nameof(voiceType), voiceType, null)
        };

        string scriptPath = Unity.AppleVoiceUnity.GetScriptPath();

        // 4) Segmente nacheinander abspielen und optionale Pause einlegen
        using (new WwiseDuckScope(Main.Settings.DuckOnPlayVolume)) // 20% Masterlautstärke
        {
            foreach (var seg in segments)
            {
                // sprechen (nur wenn Text vorhanden)
                if (!string.IsNullOrWhiteSpace(seg.Text))
                {
                    // robustes Escaping für den Shell-Aufruf
                    string escapedText = AppleVoiceUnity.EscapeForBash(seg.Text);

                    // Wir halten uns an das bisherige Aufrufschema: <script> "<voiceCmd>" "<text>"
                    string args = $"\"{voiceCmd}\" \"{escapedText}\"";

                    await Task.Run(() =>
                    {
                        using (var process = new Process())
                        {
                            process.StartInfo = new ProcessStartInfo
                            {
                                FileName = scriptPath,
                                Arguments = args,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };

                            process.Start();

                            if (Main.Settings.LogVoicedLines)
                                Main.Logger.Log($"Final Speak Command: {process.StartInfo.FileName} {process.StartInfo.Arguments}");

                            // Warten, damit Segmente seriell gesprochen werden
                            process.WaitForExit();
                        }
                    });
                }

                // optionale Pause nach dem Segment
                if (seg.PauseMs.HasValue && seg.PauseMs.Value > 0)
                {
                    await Task.Delay(seg.PauseMs.Value);
                }
                else
                {
                    await Task.Delay(100); // kurze Pause, damit nicht alles ineinanderfließt - Siribugfix
                }
            }
        }
    }

    public void Speak(string text, float delay = 0f)
    {
        // Wrapper, damit bestehende Aufrufer mit der alten Signatur weiterhin funktionieren.
        // Fire-and-forget ist hier konsistent mit dem bisherigen Verhalten von Process.Start.
        _ = SpeakAsync(text, delay);
    }
    public async Task SpeakAsync(string text, float delay = 0f)
    {
        if (string.IsNullOrEmpty(text))
        {
            Main.Logger?.Warning("No text to speak!");
            return;
        }

        text = PrepareSpeechText(text);

        await SpeakInternal(text, delay);
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