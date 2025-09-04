using System;                               // für Type und Enum.TryParse, falls nötig
using HarmonyLib;
using Kingmaker.UI;
using Kingmaker.Localization;               // LocalizedString
using Kingmaker.EntitySystem.Entities;      // UnitEntityData
using Kingmaker.Blueprints;                 // Gender
using SpeechMod.Voice;                      // VoiceType
#if DEBUG
using UnityEngine;
#endif

namespace SpeechMod.Patches
{
    // ---------- Overload 1: BarkSubtitle(UnitEntityData entity, string text, float duration = -1f) ----------
    [HarmonyPatch(typeof(UIAccess))]
[HarmonyPatch("BarkSubtitle", new[] { typeof(UnitEntityData), typeof(string), typeof(float) })]
public static class UIAccess_BarkSubtitle_String_Prefix
{
    public static void Prefix(UnitEntityData entity, string text, float duration)
    {
        if (!Main.Settings.PlayBarks || Main.Speech == null || string.IsNullOrWhiteSpace(text))
            return;
        string genderForLog = "None";
        var voice = VoiceType.Narrator;
        if (entity is UnitEntityData unit)
        {
            if (unit.Gender == Gender.Male)
            {
                voice = VoiceType.Male;
                genderForLog = "Male";
            }
            else if (unit.Gender == Gender.Female)
            {
                voice = VoiceType.Female;
                genderForLog = "Female";
            }
            // sonst bleibt Narrator
        }

        // Nur fürs TTS zusammensetzen – Parameter NICHT überschreiben
        var speakText = entity != null ? $"{entity.CharacterName}: {text}" : text;

        Main.Speech.SpeakAs(speakText, voice);

        if (Main.Settings.LogVoicedLines)
            Main.Logger.Log($"UIAccess.BarkSubtitle[string]: {speakText} (voice={genderForLog})");
    }
}


    // ---------- Overload 2: BarkSubtitle(UnitEntityData entity, LocalizedString text, float duration = -1f, bool durationByVoice = false) ----------
    [HarmonyPatch(typeof(UIAccess))]
[HarmonyPatch("BarkSubtitle", new[] { typeof(UnitEntityData), typeof(LocalizedString), typeof(float), typeof(bool) })]
public static class UIAccess_BarkSubtitle_Localized_Prefix
{
    public static void Prefix(UnitEntityData entity, LocalizedString text, float duration, bool durationByVoice)
    {
        if (!Main.Settings.PlayBarks || Main.Speech == null)
            return;
        
        string genderForLog = "None";
        var voice = VoiceType.Narrator;
        if (entity is UnitEntityData unit)
        {
            if (unit.Gender == Gender.Male)
            {
                voice = VoiceType.Male;
                genderForLog = "Male";
            }
            else if (unit.Gender == Gender.Female)
            {
                voice = VoiceType.Female;
                genderForLog = "Female";
            }
            // sonst bleibt Narrator
        }

        // LocalizedString -> string (wie im Original) nur fürs TTS zusammensetzen
        string speakText = entity != null ? $"{entity.CharacterName}: {text}" : (string)text;

        Main.Speech.SpeakAs(speakText, voice);

        if (Main.Settings.LogVoicedLines)
            Main.Logger.Log($"UIAccess.BarkSubtitle[loc]: {speakText} (voice={genderForLog})");
    }
}

}
