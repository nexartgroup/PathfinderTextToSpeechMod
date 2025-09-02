using Kingmaker;
using HarmonyLib;
using Kingmaker.UI;
using Kingmaker.EntitySystem;            // EntityDataBase
using Kingmaker.Localization;            // LocalizedString
using Kingmaker.EntitySystem.Entities; // for UnitEntityData
using Kingmaker.Blueprints;   // Gender
using SpeechMod.Voice;

namespace SpeechMod.Patches;

[HarmonyPatch(typeof(UIAccess))]
[HarmonyPatch(nameof(UIAccess.Bark),
    new[] { typeof(EntityDataBase), typeof(LocalizedString), typeof(float), typeof(bool) })]
public static class UIAccess_Bark_LocalizedString_Prefix
{
    public static void Prefix(EntityDataBase entity, LocalizedString text, float duration, bool durationByVoice)
    {
        if (Main.Settings.PlayBarks == false
            || string.IsNullOrWhiteSpace(text)
            || string.IsNullOrWhiteSpace(text)
            || Main.Speech == null)
        {
            return; // nichts zu tun
        }
        // Default: kein Gender -> Erzähler
        VoiceType voice = VoiceType.Narrator;
        string genderForLog = "None";

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

        Main.Speech.SpeakAs(text, voice);
        if (Main.Settings.LogVoicedLines)
            Main.Logger.Log($"UIAccess.Bark: {text} ({genderForLog})");
        // leer – hier kannst du später deine Logik einfügen
    }
}
