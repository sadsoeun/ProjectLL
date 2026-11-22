using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    public static partial class SceneFlowSetup
    {
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        // ------------------------------------------------------------------ popups

        private static void CreatePopupPrefabs()
        {
            NewScene(); // scratch scene so temporary objects never touch a real scene
            SavePrefab(BuildConfirmPopup(), ConfirmPopupPath);
            SavePrefab(BuildSettingsPopup(), SettingsPopupPath);
        }

        internal static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            UnityEngine.Object.DestroyImmediate(root);
            if (!success || prefab == null)
                throw new InvalidOperationException($"Failed to save prefab '{path}'.");
            Note("prefab: " + path);
            return prefab;
        }

        private static GameObject BuildConfirmPopup()
        {
            var root = UiBuilder.NewUI("ConfirmPopup", null);
            UiBuilder.Stretch(UiBuilder.Rt(root));
            var dim = root.AddComponent<Image>();
            dim.color = UiBuilder.DimColor;
            dim.raycastTarget = true;

            var window = UiBuilder.CreatePanel(root.transform, "Window", UiBuilder.WindowColor, true);
            UiBuilder.Place(UiBuilder.Rt(window.gameObject), Center, Center, Vector2.zero, new Vector2(900f, 420f));

            var message = UiBuilder.CreateLabel(window.transform, "Message", "title.confirm_quit", 44, TextAnchor.MiddleCenter);
            UiBuilder.Place(UiBuilder.Rt(message.gameObject), TopCenter, TopCenter, new Vector2(0f, -40f), new Vector2(800f, 220f));

            var yes = UiBuilder.CreateButton(window.transform, "YesButton", "common.yes", new Vector2(300f, 90f), 44);
            UiBuilder.Place(UiBuilder.Rt(yes.gameObject), BottomCenter, BottomCenter, new Vector2(-170f, 40f), new Vector2(300f, 90f));
            var no = UiBuilder.CreateButton(window.transform, "NoButton", "common.no", new Vector2(300f, 90f), 44);
            UiBuilder.Place(UiBuilder.Rt(no.gameObject), BottomCenter, BottomCenter, new Vector2(170f, 40f), new Vector2(300f, 90f));
            UiBuilder.SetNav(yes, null, null, no, no);
            UiBuilder.SetNav(no, null, null, yes, yes);

            var popup = root.AddComponent<ConfirmPopup>();
            EditorWire.Set(popup, "message", UiBuilder.Loc(message));
            EditorWire.Set(popup, "yesButton", yes);
            EditorWire.Set(popup, "noButton", no);
            return root;
        }

        private static GameObject BuildSettingsPopup()
        {
            var root = UiBuilder.NewUI("SettingsPopup", null);
            UiBuilder.Stretch(UiBuilder.Rt(root));
            var dim = root.AddComponent<Image>();
            dim.color = UiBuilder.DimColor;
            dim.raycastTarget = true;

            var window = UiBuilder.CreatePanel(root.transform, "Window", UiBuilder.WindowColor, true);
            UiBuilder.Place(UiBuilder.Rt(window.gameObject), Center, Center, Vector2.zero, new Vector2(1300f, 900f));
            var wt = window.transform;

            var title = UiBuilder.CreateLabel(wt, "Title", "settings.title", 56, TextAnchor.MiddleCenter, UiBuilder.AccentColor);
            UiBuilder.Place(UiBuilder.Rt(title.gameObject), TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(800f, 80f));

            // Language row
            var langLabel = UiBuilder.CreateLabel(wt, "LanguageLabel", "settings.language", 36, TextAnchor.MiddleLeft);
            UiBuilder.Place(UiBuilder.Rt(langLabel.gameObject), TopLeft, TopLeft, new Vector2(60f, -140f), new Vector2(300f, 70f));
            var langButtons = new List<Button>();
            for (int i = 0; i < LocalizationTable.LanguageCodes.Length; i++)
            {
                string code = LocalizationTable.LanguageCodes[i];
                var b = UiBuilder.CreateButton(wt, "Lang_" + code, null, new Vector2(170f, 70f), 30, LocalizationTable.NativeNames[i], code);
                UiBuilder.Place(UiBuilder.Rt(b.gameObject), TopLeft, TopLeft, new Vector2(370f + i * 180f, -140f), new Vector2(170f, 70f));
                langButtons.Add(b);
            }

            // Sliders
            var master = SliderRow(wt, "Master", "settings.master", -260f);
            var bgm = SliderRow(wt, "Bgm", "settings.bgm", -360f);
            var sfx = SliderRow(wt, "Sfx", "settings.sfx", -460f);

            // Toggles
            var fullscreen = UiBuilder.CreateToggle(wt, "FullscreenToggle", "settings.fullscreen", new Vector2(600f, 56f), 36);
            UiBuilder.Place(UiBuilder.Rt(fullscreen.gameObject), TopLeft, TopLeft, new Vector2(60f, -570f), new Vector2(600f, 56f));
            var vsync = UiBuilder.CreateToggle(wt, "VsyncToggle", "settings.vsync", new Vector2(600f, 56f), 36);
            UiBuilder.Place(UiBuilder.Rt(vsync.gameObject), TopLeft, TopLeft, new Vector2(60f, -650f), new Vector2(600f, 56f));

            var close = UiBuilder.CreateButton(wt, "CloseButton", "common.close", new Vector2(360f, 90f), 44);
            UiBuilder.Place(UiBuilder.Rt(close.gameObject), BottomCenter, BottomCenter, new Vector2(0f, 40f), new Vector2(360f, 90f));

            // Navigation (explicit so focus never leaves the popup)
            for (int i = 0; i < langButtons.Count; i++)
            {
                UiBuilder.SetNav(langButtons[i], close, master,
                    i > 0 ? langButtons[i - 1] : null,
                    i < langButtons.Count - 1 ? langButtons[i + 1] : null);
            }
            UiBuilder.SetNav(master, langButtons[0], bgm);
            UiBuilder.SetNav(bgm, master, sfx);
            UiBuilder.SetNav(sfx, bgm, fullscreen);
            UiBuilder.SetNav(fullscreen, sfx, vsync);
            UiBuilder.SetNav(vsync, fullscreen, close);
            UiBuilder.SetNav(close, vsync, langButtons[0]);

            var popup = root.AddComponent<SettingsPopup>();
            EditorWire.Set(popup, "languageButtons", langButtons);
            EditorWire.Set(popup, "masterSlider", master);
            EditorWire.Set(popup, "bgmSlider", bgm);
            EditorWire.Set(popup, "sfxSlider", sfx);
            EditorWire.Set(popup, "fullscreenToggle", fullscreen);
            EditorWire.Set(popup, "vsyncToggle", vsync);
            EditorWire.Set(popup, "closeButton", close);
            return root;
        }

        private static Slider SliderRow(Transform parent, string name, string labelKey, float y)
        {
            var label = UiBuilder.CreateLabel(parent, name + "Label", labelKey, 36, TextAnchor.MiddleLeft);
            UiBuilder.Place(UiBuilder.Rt(label.gameObject), TopLeft, TopLeft, new Vector2(60f, y), new Vector2(400f, 70f));
            var slider = UiBuilder.CreateSlider(parent, name + "Slider", new Vector2(740f, 50f));
            UiBuilder.Place(UiBuilder.Rt(slider.gameObject), TopLeft, TopLeft, new Vector2(480f, y - 10f), new Vector2(740f, 50f));
            return slider;
        }

        // ------------------------------------------------------------------ GameSystems

        private static void CreateGameSystemsPrefab(StageDatabase db)
        {
            NewScene();
            db = ReloadDatabase();
            var root = new GameObject("GameSystems");
            var gs = root.AddComponent<GameSystems>();

            var loader = Child<SceneLoader>(root, "SceneLoader");
            var settings = Child<SettingsService>(root, "SettingsService");
            var save = Child<SaveService>(root, "SaveService");
            var loc = Child<LocalizationService>(root, "LocalizationService");
            var input = Child<InputModeService>(root, "InputModeService");
            EditorWire.Set(input, "actions", LoadInputActions());

            // AudioService with two sources
            var audioGo = new GameObject("AudioService");
            audioGo.transform.SetParent(root.transform, false);
            var audio = audioGo.AddComponent<AudioService>();
            var bgmSource = AudioSourceChild(audioGo, "BGM", true);
            var sfxSource = AudioSourceChild(audioGo, "SFX", false);
            EditorWire.Set(audio, "bgmSource", bgmSource);
            EditorWire.Set(audio, "sfxSource", sfxSource);
            var sfxBank = AssetDatabase.LoadAssetAtPath<SfxBank>("Assets/_Project/Audio/SFX/SfxBank.asset");
            if (sfxBank != null)
                EditorWire.Set(audio, "sfxBank", sfxBank);

            // ScreenFader: top-most overlay
            var faderCanvas = UiBuilder.CreateCanvas("ScreenFader", 1000, root.transform);
            var group = faderCanvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var black = UiBuilder.CreatePanel(faderCanvas.transform, "Black", Color.black);
            UiBuilder.Stretch(UiBuilder.Rt(black.gameObject));
            black.raycastTarget = true;
            var fader = faderCanvas.gameObject.AddComponent<ScreenFader>();
            EditorWire.Set(fader, "group", group);

            // PopupCanvas: below the fader
            var popupCanvas = UiBuilder.CreateCanvas("PopupCanvas", 500, root.transform);
            var host = popupCanvas.gameObject.AddComponent<PopupHost>();
            var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPopupPath).GetComponent<SettingsPopup>();
            var confirmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmPopupPath).GetComponent<ConfirmPopup>();
            EditorWire.Set(host, "root", UiBuilder.Rt(popupCanvas.gameObject));
            EditorWire.Set(host, "settingsPopupPrefab", settingsPrefab);
            EditorWire.Set(host, "confirmPopupPrefab", confirmPrefab);

            EditorWire.Set(gs, "sceneLoader", loader);
            EditorWire.Set(gs, "screenFader", fader);
            EditorWire.Set(gs, "settingsService", settings);
            EditorWire.Set(gs, "saveService", save);
            EditorWire.Set(gs, "localizationService", loc);
            EditorWire.Set(gs, "audioService", audio);
            EditorWire.Set(gs, "inputModeService", input);
            EditorWire.Set(gs, "popupHost", host);
            EditorWire.Set(gs, "stageDatabase", db);

            SavePrefab(root, GameSystemsPath);
        }

        private static T Child<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        private static AudioSource AudioSourceChild(GameObject parent, string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            return src;
        }
    }
}
