using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    /// <summary>
    /// World map menu UI: the pause window gets Resume / Save / Load / Settings / Title / Quit, and the HUD gets an
    /// always-visible Menu button for mouse users. Idempotent: existing buttons (found by name) are reused and only
    /// laid out and wired again. Used by SceneFlowSetup when it creates 05_WorldMap, and by the menu item below for the
    /// existing scene (the scene's world art is left untouched).
    /// </summary>
    public static class WorldMapMenuSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/05_WorldMap.unity";

        private static readonly (string name, string key, string field)[] PauseButtons =
        {
            ("ResumeButton", "pause.resume", "resumeButton"),
            ("SaveButton", "pause.save", "saveButton"),
            ("LoadButton", "pause.load", "loadButton"),
            ("SettingsButton", "pause.settings", "settingsButton"),
            ("TitleButton", "pause.to_title", "titleButton"),
            ("QuitButton", "pause.quit", "quitButton"),
        };

        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private const float ButtonWidth = 560f, ButtonHeight = 84f, ButtonStep = 96f, FirstButtonY = -150f; // window top stays below the message line

        [MenuItem("Tools/MyGame/World Map/Setup Menu Buttons")]
        public static void Menu() => Debug.Log(RunOnScene());

        /// <summary>Opens 05_WorldMap, applies the menu UI and saves the scene. Returns a report.</summary>
        public static string RunOnScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before setting up the world map menu.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var ctrl = UnityEngine.Object.FindFirstObjectByType<WorldMapController>()
                       ?? throw new InvalidOperationException("05_WorldMap has no WorldMapController.");
            string report = Apply(ctrl);
            GoldUiTheme.ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return report;
        }

        /// <summary>Creates or re-lays the menu buttons below the controller's pause panel and wires them.</summary>
        public static string Apply(WorldMapController ctrl)
        {
            var so = new SerializedObject(ctrl);
            var pause = so.FindProperty("pausePanel").objectReferenceValue as GameObject
                        ?? throw new InvalidOperationException("WorldMapController.pausePanel is not assigned.");
            var window = pause.transform.Find("Window") as RectTransform
                         ?? throw new InvalidOperationException("Pause menu has no 'Window' child.");
            var canvas = pause.transform.parent;
            var log = new StringBuilder("[WorldMapMenuSetup]\n");

            // Pause window: six buttons in a column.
            var buttons = new List<Selectable>();
            for (int i = 0; i < PauseButtons.Length; i++)
            {
                var (name, key, field) = PauseButtons[i];
                var t = window.Find(name);
                Button b = t != null ? t.GetComponent<Button>() : null;
                if (b == null)
                {
                    b = UiBuilder.CreateButton(window, name, key, new Vector2(ButtonWidth, ButtonHeight), 40);
                    log.AppendLine("  created " + name);
                }
                UiBuilder.Place((RectTransform)b.transform, TopCenter, TopCenter, new Vector2(0f, FirstButtonY - i * ButtonStep),
                    new Vector2(ButtonWidth, ButtonHeight));
                b.transform.SetSiblingIndex(window.childCount - 1);
                EditorWire.Set(ctrl, field, b);
                buttons.Add(b);
            }
            UiBuilder.LinkVertical(buttons, true);
            float height = -FirstButtonY + (PauseButtons.Length - 1) * ButtonStep + ButtonHeight + 50f;
            UiBuilder.Place(window, Center, Center, Vector2.zero, new Vector2(760f, height));

            // HUD Menu button (top right). The hint line is narrowed so they do not overlap.
            var menuT = canvas.Find("MenuButton");
            Button menu = menuT != null ? menuT.GetComponent<Button>() : null;
            if (menu == null)
            {
                menu = UiBuilder.CreateButton(canvas, "MenuButton", "worldmap.menu", new Vector2(220f, 76f), 36);
                log.AppendLine("  created MenuButton");
            }
            UiBuilder.Place((RectTransform)menu.transform, TopRight, TopRight, new Vector2(-24f, -16f), new Vector2(220f, 76f));
            var nav = menu.navigation;
            nav.mode = Navigation.Mode.None; // HUD button is for the mouse; pads use Start
            menu.navigation = nav;
            EditorWire.Set(ctrl, "menuButton", menu);

            var hint = canvas.Find("HintText") as RectTransform;
            if (hint != null)
            {
                UiBuilder.Place(hint, TopCenter, TopCenter, new Vector2(-130f, -32f), new Vector2(1560f, 44f));
                var text = hint.GetComponent<Text>();
                if (text != null)
                {
                    text.resizeTextForBestFit = true;
                    text.resizeTextMinSize = 16;
                    text.resizeTextMaxSize = 26;
                }
            }

            // Messages ("Saved." etc.) must stay readable above the pause dim; the menu button sits below the dim.
            // Fixed draw order on every run: ... HUD, MenuButton, PauseMenu (dim), MessageText.
            menu.transform.SetAsLastSibling();
            pause.transform.SetAsLastSibling();
            var message = canvas.Find("MessageText");
            if (message != null)
                message.SetAsLastSibling();

            log.Append($"  pause buttons {buttons.Count}, window height {height:0}, menu button wired");
            return log.ToString();
        }
    }
}
