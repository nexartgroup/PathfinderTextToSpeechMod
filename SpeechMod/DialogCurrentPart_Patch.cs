﻿using HarmonyLib;
using Kingmaker;
using Kingmaker.UI;
using Owlcat.Runtime.UI.Controls.Button;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SpeechMod
{
    [HarmonyPatch(typeof(StaticCanvas), "Initialize")]
    static class DialogCurrentPart_Patch
    {
        static void Postfix()
        {
            if (!Main.Enabled)
                return;

            Debug.Log("Speech Mod Initializing...");

            var parent = Game.Instance.UI.Canvas.transform.Find("DialogPCView/Body/View/Scroll View");
            var originalButton = Game.Instance.UI.Canvas.transform.Find("DialogPCView/Body/View/Scroll View/ButtonEdge").gameObject;

            var buttonGameObject = GameObject.Instantiate(originalButton, parent);
            buttonGameObject.name = "SpeechButton";
            buttonGameObject.transform.localPosition = new Vector3(-493, 164, 0);
            buttonGameObject.transform.localRotation = Quaternion.Euler(0, 0, 90);

            buttonGameObject.AddComponent<WindowsVoice>();

            var button = buttonGameObject.GetComponent<OwlcatButton>();
            button.OnLeftClick.RemoveAllListeners();
            button.OnLeftClick.SetPersistentListenerState(0, UnityEventCallState.Off);
            button.OnLeftClick.AddListener(Speak);

            buttonGameObject.SetActive(true);

            Debug.Log("Speech Mod Initialized!");
        }

        private static void Speak()
        {
            var text = Game.Instance?.DialogController?.CurrentCue?.DisplayText;
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogWarning("No display text in the curren cue of the dialog controller!");
                return;
            }

            // TODO: Load replaces into a dictionary from a json file so they can be added and altered more easily.
            foreach (var pair in _phoneticalDictionary)
            {
                text = text.Replace(pair.Key, pair.Value);
            }

            WindowsVoice.speak(text);
        }

        private static readonly Dictionary<string, string> _phoneticalDictionary = new Dictionary<string, string>()
        {
            { "—", "," },
            { "Kenabres", "Keenaaabres" },
            { "Iomedae", "I,omedaee" },
            { "Golarion", "Goolaarion" },
            { "Sovyrian", "Sovyyrian" }
        };
    }
}
