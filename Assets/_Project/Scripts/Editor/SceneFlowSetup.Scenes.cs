using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    public static partial class SceneFlowSetup
    {
        private static readonly Color NavyBackground = new Color(0.07f, 0.09f, 0.15f);
        private static readonly Color SkyColor = new Color(0.55f, 0.75f, 0.95f);

        private static void At(Component c, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            UiBuilder.Place((RectTransform)c.transform, anchor, pivot, position, size);
        }

        // ------------------------------------------------------------------ 01_Logo

        private static void CreateLogoScene()
        {
            var scene = NewScene();
            ConfigureAmbient();
            CreateCamera("Main Camera", Color.black, new Vector3(0f, 0f, -10f), Vector3.zero);

            var canvas = UiBuilder.CreateCanvas("LogoCanvas", 0);
            var groupGo = UiBuilder.NewUI("LogoGroup", canvas.transform);
            UiBuilder.Stretch(UiBuilder.Rt(groupGo));
            var group = groupGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var logoImage = UiBuilder.CreatePanel(groupGo.transform, "LogoImage", Color.white, false);
            At(logoImage, Center, Center, Vector2.zero, new Vector2(1050f, 700f));
            logoImage.sprite = Resources.Load<Sprite>(LogoSequencer.DefaultLogoResourcePath);
            logoImage.type = UnityEngine.UI.Image.Type.Simple;
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;

            var seq = new GameObject("LogoSequencer").AddComponent<LogoSequencer>();
            EditorWire.Set(seq, "logoGroup", group);
            EditorWire.Set(seq, "logoImage", logoImage);
            EditorWire.Set(seq, "logoSprites", new[] { logoImage.sprite });

            SaveScene(scene, ScenesDir + "/01_Logo.unity");
        }

        // ------------------------------------------------------------------ 02_LanguageSelect

        private static void CreateLanguageSelectScene()
        {
            var scene = NewScene();
            ConfigureAmbient();
            CreateCamera("Main Camera", NavyBackground, new Vector3(0f, 0f, -10f), Vector3.zero);

            var canvas = UiBuilder.CreateCanvas("LanguageCanvas", 0);
            var header = UiBuilder.CreateLabel(canvas.transform, "Header", "lang.header", 72, TextAnchor.MiddleCenter, UiBuilder.AccentColor);
            At(header, TopCenter, TopCenter, new Vector2(0f, -60f), new Vector2(1600f, 120f));

            var list = UiBuilder.NewUI("LanguageList", canvas.transform);
            At(list.GetComponent<RectTransform>(), TopCenter, TopCenter, new Vector2(0f, -220f), new Vector2(760f, 640f));

            var langButtons = new List<LanguageButton>();
            var selectables = new List<Selectable>();
            for (int i = 0; i < LocalizationTable.LanguageCodes.Length; i++)
            {
                string code = LocalizationTable.LanguageCodes[i];
                var b = UiBuilder.CreateButton(list.transform, "Lang_" + code, null, new Vector2(700f, 100f), 48,
                    LocalizationTable.NativeNames[i], code);
                At(b, TopCenter, TopCenter, new Vector2(0f, -i * 124f), new Vector2(700f, 100f));
                var lb = b.gameObject.AddComponent<LanguageButton>();
                EditorWire.Set(lb, "languageCode", code);
                langButtons.Add(lb);
                selectables.Add(b);
            }

            var confirm = UiBuilder.CreateButton(canvas.transform, "ConfirmButton", "lang.confirm", new Vector2(500f, 100f), 48);
            At(confirm, BottomCenter, BottomCenter, new Vector2(0f, 90f), new Vector2(500f, 100f));
            selectables.Add(confirm);
            UiBuilder.LinkVertical(selectables, false);

            var error = UiBuilder.CreateLabel(canvas.transform, "ErrorText", "lang.save_failed", 34, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.45f));
            At(error, BottomCenter, BottomCenter, new Vector2(0f, 200f), new Vector2(1400f, 50f));
            error.gameObject.SetActive(false);

            var keeper = UiBuilder.CreateEventSystem(selectables[0].gameObject, true);

            var ctrl = new GameObject("LanguageSelectController").AddComponent<LanguageSelectController>();
            EditorWire.Set(ctrl, "languageButtons", langButtons);
            EditorWire.Set(ctrl, "confirmButton", confirm);
            EditorWire.Set(ctrl, "errorText", UiBuilder.Loc(error));
            EditorWire.Set(ctrl, "focus", keeper);

            SaveScene(scene, ScenesDir + "/02_LanguageSelect.unity");
        }

        // ------------------------------------------------------------------ 03_Title

        private static void CreateTitleScene(Materials mats)
        {
            var scene = NewScene();
            ConfigureAmbient();
            CreateCamera("Main Camera", SkyColor, new Vector3(0f, 3f, -14f), new Vector3(8f, 0f, 0f));
            CreateDirectionalLight();

            var env = new GameObject("Environment").transform;
            TitleBackdrop.Build(env); // Green Meadow diorama (ground top at y = 0)
            // Mascot: original Leaf Explorer idling on the ground, turned slightly toward the menu.
            var mascot = InstantiatePlayerVisual(scene, env);
            mascot.name = "Mascot";
            mascot.transform.localScale = Vector3.one * 2.5f;
            // The player controller needs gameplay parameters (Grounded = false plays the fall loop); the mascot only idles.
            mascot.GetComponentInChildren<Animator>().runtimeAnimatorController = TitleIdleController();
            ArrangeTitleShowcase(env);

            var canvas = UiBuilder.CreateCanvas("TitleCanvas", 0);
            var logo = UiBuilder.CreateLabel(canvas.transform, "GameLogo", null, 150, TextAnchor.MiddleCenter, Color.white, "LEAF & LEAP", "en", FontStyle.Bold);
            At(logo, TopCenter, TopCenter, new Vector2(0f, -70f), new Vector2(1600f, 200f));

            var menu = UiBuilder.NewUI("MainMenu", canvas.transform);
            At(menu.GetComponent<RectTransform>(), Center, Center, new Vector2(0f, -150f), new Vector2(560f, 500f));
            string[] keys = { "title.new_game", "title.continue", "title.settings", "title.quit" };
            var buttons = new List<Button>();
            var sel = new List<Selectable>();
            for (int i = 0; i < keys.Length; i++)
            {
                var b = UiBuilder.CreateButton(menu.transform, "Button_" + keys[i].Substring(6), keys[i], new Vector2(520f, 100f), 48);
                At(b, TopCenter, TopCenter, new Vector2(0f, -i * 125f), new Vector2(520f, 100f));
                buttons.Add(b);
                sel.Add(b);
            }
            UiBuilder.LinkVertical(sel, true);

            var notice = UiBuilder.CreateLabel(canvas.transform, "NoticeText", "title.save_corrupted", 36, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.45f));
            At(notice, BottomCenter, BottomCenter, new Vector2(0f, 90f), new Vector2(1600f, 60f));
            notice.gameObject.SetActive(false);

            var version = UiBuilder.CreateLabel(canvas.transform, "VersionText", null, 28, TextAnchor.LowerRight, UiBuilder.SubTextColor, "v0.1", "en");
            At(version, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(400f, 50f));

            var keeper = UiBuilder.CreateEventSystem(buttons[0].gameObject, true);

            var ctrl = new GameObject("TitleController").AddComponent<TitleController>();
            EditorWire.Set(ctrl, "newGameButton", buttons[0]);
            EditorWire.Set(ctrl, "continueButton", buttons[1]);
            EditorWire.Set(ctrl, "settingsButton", buttons[2]);
            EditorWire.Set(ctrl, "quitButton", buttons[3]);
            EditorWire.Set(ctrl, "noticeText", UiBuilder.Loc(notice));
            EditorWire.Set(ctrl, "versionText", version);
            EditorWire.Set(ctrl, "bgm", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/BGM/BGM_Title.mp3"));
            EditorWire.Set(ctrl, "focus", keeper);

            SaveScene(scene, ScenesDir + "/03_Title.unity");
        }

        // ------------------------------------------------------------------ 04_Loading

        private static void CreateLoadingScene()
        {
            var scene = NewScene();
            ConfigureAmbient();
            CreateCamera("Main Camera", NavyBackground, new Vector3(0f, 0f, -10f), Vector3.zero);

            var canvas = UiBuilder.CreateCanvas("LoadingCanvas", 0);
            var bg = UiBuilder.CreatePanel(canvas.transform, "Background", NavyBackground);
            UiBuilder.Stretch(UiBuilder.Rt(bg.gameObject));

            var icon = UiBuilder.CreatePanel(canvas.transform, "LoadingIcon", UiBuilder.AccentColor, true);
            At(icon, Center, Center, new Vector2(0f, 80f), new Vector2(110f, 110f));

            var loadingLabel = UiBuilder.CreateLabel(canvas.transform, "LoadingLabel", "loading.loading", 60, TextAnchor.MiddleCenter);
            At(loadingLabel, Center, Center, new Vector2(0f, -60f), new Vector2(900f, 90f));

            var tip = UiBuilder.CreateLabel(canvas.transform, "TipText", "loading.tip1", 36, TextAnchor.MiddleCenter, UiBuilder.SubTextColor);
            At(tip, BottomCenter, BottomCenter, new Vector2(0f, 200f), new Vector2(1500f, 90f));

            var barBack = UiBuilder.CreatePanel(canvas.transform, "ProgressBar", UiBuilder.BarBackColor, true);
            At(barBack, BottomCenter, BottomCenter, new Vector2(0f, 130f), new Vector2(1100f, 30f));
            var fill = UiBuilder.CreatePanel(barBack.transform, "ProgressFill", UiBuilder.AccentColor, true);
            var fillRt = UiBuilder.Rt(fill.gameObject);
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var ctrl = new GameObject("LoadingController").AddComponent<LoadingController>();
            EditorWire.Set(ctrl, "progressFill", fillRt);
            EditorWire.Set(ctrl, "loadingIcon", UiBuilder.Rt(icon.gameObject));
            EditorWire.Set(ctrl, "tipText", UiBuilder.Loc(tip));

            SaveScene(scene, ScenesDir + "/04_Loading.unity");
        }

        // ------------------------------------------------------------------ 05_WorldMap

        private static readonly Vector3 WorldMapCameraOffset = new Vector3(0f, 9f, -7f);

        // Original Leaf Explorer model and native Generic gameplay animations.
        private const string PlayerVisualPrefabPath = "Assets/_Project/Art/Characters/LeafExplorer/LeafExplorer_Visual.prefab";
        // The explorer is approximately 1 m tall; scale to fill the existing 2 m gameplay capsule.
        private const float PlayerVisualScale = 1.92f;
        /// <summary>Material of the dodge afterimages (MyGame/FX/Afterimage), created by <see cref="EnsureDodgeAfterimage"/>.</summary>
        internal const string DodgeAfterimageMaterialPath = "Assets/_Project/Art/Materials/FX/M_DodgeAfterimage.mat";

        /// <summary>Creates the afterimage material if needed and attaches DodgeAfterimage to the InGame player (open scene).</summary>
        internal static string EnsureDodgeAfterimage()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DodgeAfterimageMaterialPath);
            if (mat == null)
            {
                var shader = Shader.Find("MyGame/FX/Afterimage");
                if (shader == null)
                    return "shader MyGame/FX/Afterimage not found";
                EnsureFolder("Assets/_Project/Art/Materials/FX");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, DodgeAfterimageMaterialPath);
            }
            var motor = Object.FindAnyObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            if (motor == null)
                return "material ok; no PlayerMotor in the open scene";
            var comp = motor.GetComponent<DodgeAfterimage>();
            if (comp == null)
                comp = motor.gameObject.AddComponent<DodgeAfterimage>();
            EditorWire.Set(comp, "ghostMaterial", mat);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(motor.gameObject.scene);
            return $"DodgeAfterimage on {motor.name} in {motor.gameObject.scene.name}";
        }
        // World map avatar: about the size of the old 1.2 m placeholder, next to 1 m wide stage nodes.
        private const float MapPlayerVisualScale = 1.25f;

        private const string MonsterVisualDir = "Assets/_Project/Prefabs/Monsters/Stage01/";

        /// <summary>
        /// Title composition (camera at z = -14 looking down +Z; the menu covers the left ~37% of the screen):
        /// the mascot stands at ~70% of the width, a Sprout Bean strolls through the centre under the floating relic
        /// and an Acorn Snail idles on the right. Idempotent, so it also updates an existing title scene.
        /// </summary>
        internal static void ArrangeTitleShowcase(Transform env)
        {
            var mascot = env.Find("Mascot");
            if (mascot != null)
            {
                mascot.localPosition = new Vector3(3.6f, 0f, -2f);
                mascot.localRotation = Quaternion.Euler(0f, 200f, 0f);
            }
            TitleCritter(env, "TitleSproutBean", "SproutBean_Visual", new Vector3(0.2f, 0f, -1.2f), 2.0f, true, -0.6f, 1.0f);
            TitleCritter(env, "TitleAcornSnail", "AcornSnail_Visual", new Vector3(6.4f, 0f, -0.6f), 2.0f, false, 0f, 0f);
        }

        private static void TitleCritter(Transform env, string name, string visual, Vector3 position, float scale, bool walks, float minX, float maxX)
        {
            var old = env.Find(name);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterVisualDir + visual + ".prefab");
            if (prefab == null)
                return; // Stage01 monster art not imported yet
            var root = new GameObject(name).transform;
            root.SetParent(env, false);
            root.localPosition = position;
            root.localScale = Vector3.one * scale;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var critter = root.gameObject.AddComponent<MyGame.TitleCritter>();
            EditorWire.Set(critter, "walks", walks);
            EditorWire.Set(critter, "minX", minX);
            EditorWire.Set(critter, "maxX", maxX);
            root.localRotation = Quaternion.Euler(0f, walks ? 125f : 180f, 0f);
        }

        private const string TitleIdleControllerPath ="Assets/_Project/Art/Characters/LeafExplorer/LeafExplorer_TitleIdle.controller";
        private const string ExplorerMotionFbxPath = "Assets/_Project/Art/Characters/LeafExplorer/LeafExplorer.fbx";

        /// <summary>Idle-only controller for the original Leaf Explorer; created once.</summary>
        internal static RuntimeAnimatorController TitleIdleController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TitleIdleControllerPath);
            if (existing != null)
                return existing;
            var clip = AssetDatabase.LoadAllAssetsAtPath(ExplorerMotionFbxPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Idle")
                       ?? throw new System.InvalidOperationException("Idle not found in " + ExplorerMotionFbxPath);
            return UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(TitleIdleControllerPath, clip);
        }

        private static GameObject InstantiatePlayerVisual(UnityEngine.SceneManagement.Scene scene, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVisualPrefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException("Player visual prefab missing at " + PlayerVisualPrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void CreateWorldMapScene(StageDatabase db, Materials mats)
        {
            var scene = NewScene();
            db = ReloadDatabase();
            ConfigureAmbient();
            var cam = CreateCamera("Main Camera", SkyColor, WorldMapCameraOffset, new Vector3(55f, 0f, 0f));
            CreateDirectionalLight();

            var env = new GameObject("MapEnvironment").transform;
            CreatePrimitive(PrimitiveType.Cube, "MapGround", env, new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 18f), mats.MapGround, false);

            // Nodes
            var nodesRoot = new GameObject("Nodes").transform;
            var nodes = new List<WorldMapNode>();
            foreach (var data in db.Nodes)
                nodes.Add(BuildMapNode(data, nodesRoot, mats));

            // Paths (one per connected pair)
            var pathsRoot = new GameObject("Paths").transform;
            var paths = new List<WorldMapPath>();
            var done = new HashSet<string>();
            foreach (var data in db.Nodes)
            {
                foreach (MapDirection d in new[] { MapDirection.Up, MapDirection.Down, MapDirection.Left, MapDirection.Right })
                {
                    string other = data.GetNeighborId(d);
                    if (string.IsNullOrEmpty(other) || !db.TryGetNode(other, out var otherData))
                        continue;
                    string key = string.CompareOrdinal(data.NodeId, other) < 0 ? data.NodeId + "|" + other : other + "|" + data.NodeId;
                    if (!done.Add(key))
                        continue;
                    paths.Add(BuildMapPath(data, otherData, pathsRoot, mats));
                }
            }

            // Avatar: movement root + separate visual child
            if (!db.TryGetNode(db.StartNodeId, out var startNode))
            {
                var ids = string.Join(",", db.Nodes.Select(n => n == null ? "<null>" : $"'{n.NodeId}'"));
                throw new System.InvalidOperationException(
                    $"Start node '{db.StartNodeId}' not found. db alive={db != null}, nodes=[{ids}]");
            }
            var playerGo = new GameObject("MapPlayer");
            playerGo.transform.position = startNode.MapPosition;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(playerGo.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.15f, 0f); // stand on the 0.15 m node marker
            var mapCharacter = InstantiatePlayerVisual(scene, visual.transform);
            mapCharacter.transform.localScale = Vector3.one * MapPlayerVisualScale;
            var mapPlayer = playerGo.AddComponent<MapPlayer>();
            EditorWire.Set(mapPlayer, "visual", visual.transform);
            EditorWire.Set(mapPlayer, "animator", mapCharacter.GetComponent<Animator>());
            EditorWire.Set(mapPlayer, "bobHeight", 0f); // the run animation replaces the placeholder bob

            var follow = cam.gameObject.AddComponent<CameraFollow>();
            EditorWire.Set(follow, "offset", WorldMapCameraOffset);
            EditorWire.Set(follow, "smoothTime", 0.2f);

            // UI
            var canvas = UiBuilder.CreateCanvas("WorldMapCanvas", 0);

            var hint = UiBuilder.CreateLabel(canvas.transform, "HintText", "worldmap.hint", 26, TextAnchor.MiddleCenter, UiBuilder.SubTextColor);
            At(hint, TopCenter, TopCenter, new Vector2(0f, -20f), new Vector2(1800f, 40f));
            var message = UiBuilder.CreateLabel(canvas.transform, "MessageText", "worldmap.locked_msg", 48, TextAnchor.MiddleCenter, UiBuilder.AccentColor);
            At(message, TopCenter, TopCenter, new Vector2(0f, -100f), new Vector2(1500f, 70f));

            var info = UiBuilder.CreatePanel(canvas.transform, "StageInfoPanel", new Color(0.08f, 0.1f, 0.17f, 0.9f), true);
            At(info, BottomCenter, BottomCenter, new Vector2(0f, 40f), new Vector2(1200f, 290f));
            var stageName = UiBuilder.CreateLabel(info.transform, "StageNameText", "stage.W1_S1.name", 54, TextAnchor.MiddleLeft);
            At(stageName, TopLeft, TopLeft, new Vector2(40f, -20f), new Vector2(760f, 70f));
            var state = UiBuilder.CreateLabel(info.transform, "StateText", "worldmap.state.open", 38, TextAnchor.MiddleLeft, UiBuilder.AccentColor);
            At(state, TopLeft, TopLeft, new Vector2(40f, -100f), new Vector2(500f, 50f));
            var best = UiBuilder.CreateLabel(info.transform, "BestTimeText", "worldmap.no_record", 34, TextAnchor.MiddleLeft, UiBuilder.SubTextColor);
            At(best, TopLeft, TopLeft, new Vector2(40f, -155f), new Vector2(700f, 50f));
            var specialsLabel = UiBuilder.CreateLabel(info.transform, "SpecialsText", "worldmap.specials", 34, TextAnchor.MiddleLeft, UiBuilder.SubTextColor);
            At(specialsLabel, TopLeft, TopLeft, new Vector2(40f, -215f), new Vector2(560f, 50f));
            var mapSlots = CreateSpecialSlots(info.transform, "SpecialSlots", TopLeft, new Vector2(660f, -211f), 36f, 56f);
            var enter = UiBuilder.CreateButton(info.transform, "EnterButton", "worldmap.enter", new Vector2(340f, 110f), 40);
            At(enter, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(340f, 110f));

            // Pause menu (hidden until Esc)
            var pauseDim = UiBuilder.CreateDim(canvas.transform, "PauseMenu");
            var window = UiBuilder.CreatePanel(pauseDim.transform, "Window", UiBuilder.WindowColor, true);
            At(window, Center, Center, Vector2.zero, new Vector2(760f, 640f));
            var pauseTitle = UiBuilder.CreateLabel(window.transform, "Title", "pause.title", 60, TextAnchor.MiddleCenter, UiBuilder.AccentColor);
            At(pauseTitle, TopCenter, TopCenter, new Vector2(0f, -40f), new Vector2(600f, 80f));
            var resume = UiBuilder.CreateButton(window.transform, "ResumeButton", "pause.resume", new Vector2(560f, 100f), 44);
            At(resume, TopCenter, TopCenter, new Vector2(0f, -170f), new Vector2(560f, 100f));
            var settingsBtn = UiBuilder.CreateButton(window.transform, "SettingsButton", "pause.settings", new Vector2(560f, 100f), 44);
            At(settingsBtn, TopCenter, TopCenter, new Vector2(0f, -300f), new Vector2(560f, 100f));
            var titleBtn = UiBuilder.CreateButton(window.transform, "TitleButton", "pause.to_title", new Vector2(560f, 100f), 44);
            At(titleBtn, TopCenter, TopCenter, new Vector2(0f, -430f), new Vector2(560f, 100f));
            UiBuilder.LinkVertical(new List<Selectable> { resume, settingsBtn, titleBtn }, true);
            pauseDim.gameObject.SetActive(false);

            var keeper = UiBuilder.CreateEventSystem(null, false);

            var ctrl = new GameObject("WorldMapController").AddComponent<WorldMapController>();
            EditorWire.Set(ctrl, "nodes", nodes);
            EditorWire.Set(ctrl, "paths", paths);
            EditorWire.Set(ctrl, "mapPlayer", mapPlayer);
            EditorWire.Set(ctrl, "cameraFollow", follow);
            EditorWire.Set(ctrl, "bgm", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/BGM/BGM_Main.mp3"));
            WireWorldBgms(ctrl);
            // BGM follows the world terrain under the avatar (Terrain_R0N_Walk colliders).
            var groundRegion = ctrl.gameObject.AddComponent<WorldMapGroundRegion>();
            EditorWire.Set(groundRegion, "target", mapPlayer.transform);
            EditorWire.Set(ctrl, "groundRegion", groundRegion);
            EditorWire.Set(ctrl, "stageNameText", UiBuilder.Loc(stageName));
            EditorWire.Set(ctrl, "stateText", UiBuilder.Loc(state));
            EditorWire.Set(ctrl, "bestTimeText", UiBuilder.Loc(best));
            EditorWire.Set(ctrl, "specialsText", UiBuilder.Loc(specialsLabel));
            EditorWire.Set(ctrl, "specialSlots", mapSlots);
            EditorWire.Set(ctrl, "messageText", UiBuilder.Loc(message));
            EditorWire.Set(ctrl, "enterButton", enter);
            EditorWire.Set(ctrl, "pausePanel", pauseDim.gameObject);
            EditorWire.Set(ctrl, "resumeButton", resume);
            EditorWire.Set(ctrl, "settingsButton", settingsBtn);
            EditorWire.Set(ctrl, "titleButton", titleBtn);
            EditorWire.Set(ctrl, "focus", keeper);
            WorldMapMenuSetup.Apply(ctrl); // save / load / quit buttons + HUD menu button
            WorldMapLightingSetup.Apply(); // region lighting + grading
            SaveScene(scene, ScenesDir + "/05_WorldMap.unity");
        }

        private static WorldMapNode BuildMapNode(WorldMapNodeData data, Transform parent, Materials mats)
        {
            var go = new GameObject("Node_" + data.NodeId);
            go.transform.SetParent(parent, false);
            go.transform.position = data.MapPosition;

            var flag = new GameObject("Flag");
            flag.transform.SetParent(go.transform, false);
            flag.transform.localPosition = new Vector3(0.35f, 0f, 0.5f); // back-right edge, clear of the avatar
            CreatePrimitive(PrimitiveType.Cylinder, "Pole", flag.transform, new Vector3(0.3f, 0.75f, 0f), new Vector3(0.06f, 0.6f, 0.06f), mats.Path, false);
            CreatePrimitive(PrimitiveType.Cube, "Banner", flag.transform, new Vector3(0.5f, 1.15f, 0f), new Vector3(0.4f, 0.25f, 0.04f), mats.Flag, false);
            flag.SetActive(false);

            var node = go.AddComponent<WorldMapNode>();
            EditorWire.Set(node, "data", data);
            EditorWire.Set(node, "clearedFlag", flag);
            WorldMapNodePads.Attach(node, data.NodeId.EndsWith("_B", System.StringComparison.Ordinal));
            return node;
        }

        /// <summary>Per-world map BGM (node-id prefix → clip). Worlds without an entry use the fallback bgm.</summary>
        internal static readonly (string prefix, string clipPath)[] WorldBgmSpecs =
        {
            ("W1", "Assets/_Project/Audio/BGM/BGM_World1.mp3"),
            ("W2", "Assets/_Project/Audio/BGM/BGM_World2.mp3"),
            ("W3", "Assets/_Project/Audio/BGM/BGM_World3.mp3"),
            ("W4", "Assets/_Project/Audio/BGM/BGM_World4.mp3"),
            ("W5", "Assets/_Project/Audio/BGM/BGM_World5.mp3"),
        };

        internal static void WireWorldBgms(WorldMapController ctrl)
        {
            var so = new SerializedObject(ctrl);
            var list = so.FindProperty("worldBgms");
            list.arraySize = 0;
            foreach (var (prefix, clipPath) in WorldBgmSpecs)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip == null)
                {
                    Debug.LogWarning($"[SceneFlowSetup] World BGM missing: {clipPath}");
                    continue;
                }
                int i = list.arraySize++;
                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("nodeIdPrefix").stringValue = prefix;
                el.FindPropertyRelative("clip").objectReferenceValue = clip;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static WorldMapPath BuildMapPath(WorldMapNodeData a, WorldMapNodeData b, Transform parent, Materials mats)
        {
            Vector3 pa = a.MapPosition;
            Vector3 pb = b.MapPosition;
            Vector3 mid = (pa + pb) * 0.5f + new Vector3(0f, 0.03f, 0f);
            float dist = Vector3.Distance(pa, pb);
            var go = CreatePrimitive(PrimitiveType.Cube, $"Path_{a.NodeId}_{b.NodeId}", parent, mid, new Vector3(0.4f, 0.06f, dist), mats.Path, false);
            go.transform.rotation = Quaternion.LookRotation((pb - pa).normalized, Vector3.up);
            var path = go.AddComponent<WorldMapPath>();
            EditorWire.Set(path, "fromNodeId", a.NodeId);
            EditorWire.Set(path, "toNodeId", b.NodeId);
            return path;
        }

        /// <summary>Row of three diamond-shaped slot images (special collectible slots), drawn empty.</summary>
        private static List<Image> CreateSpecialSlots(Transform parent, string name, Vector2 anchor, Vector2 position, float size, float spacing)
        {
            var row = UiBuilder.NewUI(name, parent);
            At(row.GetComponent<RectTransform>(), anchor, anchor == TopCenter ? TopCenter : TopLeft, position, new Vector2(spacing * 3f, size * 1.6f));
            var slots = new List<Image>();
            for (int i = 0; i < StageSession.SpecialSlotCount; i++)
            {
                var img = UiBuilder.CreatePanel(row.transform, "Slot" + (i + 1), HudColors.SpecialEmpty);
                var rt = UiBuilder.Rt(img.gameObject);
                At(img, Center, Center, new Vector2((i - 1) * spacing, 0f), new Vector2(size, size));
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                slots.Add(img);
            }
            return slots;
        }

        // ------------------------------------------------------------------ 06_InGame

        private static void CreateInGameScene(Materials mats)
        {
            var scene = NewScene();
            ConfigureAmbient();

            // Farther and wider side view so more of the course ahead and above is visible.
            var cam = CreateCamera("GameplayCamera", SkyColor, new Vector3(0f, 3f, -16f), new Vector3(3f, 0f, 0f), 50f);
            var follow = cam.gameObject.AddComponent<CameraFollow>();
            EditorWire.Set(follow, "offset", new Vector3(0f, 2f, -16f));
            EditorWire.Set(follow, "smoothTime", 0.15f);

            // Player: movement root (physics) + separate visual model
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 1.2f, 0f);
            var rb = player.AddComponent<Rigidbody>();
            rb.mass = 1f;
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = 0.5f;
            capsule.height = 2f;
            capsule.center = Vector3.zero;
            capsule.sharedMaterial = mats.PlayerPhysics;

            // Visual root turns with the facing direction; the Leaf Explorer model stands on the capsule bottom.
            var visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(player.transform, false);
            visualRoot.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var character = InstantiatePlayerVisual(scene, visualRoot.transform);
            character.transform.localPosition = new Vector3(0f, -capsule.height * 0.5f, 0f);
            character.transform.localScale = Vector3.one * PlayerVisualScale;

            var motor = player.AddComponent<PlayerMotor>();
            EditorWire.Set(motor, "visual", visualRoot.transform);
            player.AddComponent<PlayerInputReader>();
            var afterimage = player.AddComponent<DodgeAfterimage>();
            EditorWire.Set(afterimage, "ghostMaterial", AssetDatabase.LoadAssetAtPath<Material>(DodgeAfterimageMaterialPath));
            var playerAnimator = player.AddComponent<PlayerAnimator>();
            EditorWire.Set(playerAnimator, "animator", character.GetComponent<Animator>());

            // HUD
            var canvas = UiBuilder.CreateCanvas("HUDCanvas", 0);
            var view = canvas.gameObject.AddComponent<InGameView>();

            var lives = UiBuilder.CreateLabel(canvas.transform, "LivesText", "ingame.lives", 46, TextAnchor.MiddleLeft);
            At(lives, TopLeft, TopLeft, new Vector2(40f, -30f), new Vector2(500f, 70f));
            var time = UiBuilder.CreateLabel(canvas.transform, "TimeText", "ingame.time", 46, TextAnchor.MiddleRight);
            At(time, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(500f, 70f));
            var stageName = UiBuilder.CreateLabel(canvas.transform, "StageNameText", "stage.W1_S1.name", 40, TextAnchor.MiddleCenter, UiBuilder.SubTextColor);
            At(stageName, TopCenter, TopCenter, new Vector2(0f, -35f), new Vector2(800f, 60f));
            var coins = UiBuilder.CreateLabel(canvas.transform, "CoinsText", "ingame.coins", 40, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.2f));
            At(coins, TopLeft, TopLeft, new Vector2(40f, -100f), new Vector2(500f, 56f));
            var hudSlots = CreateSpecialSlots(canvas.transform, "SpecialSlots", TopCenter, new Vector2(0f, -150f), 44f, 70f);
            var message = UiBuilder.CreateLabel(canvas.transform, "MessageText", "ingame.ready", 120, TextAnchor.MiddleCenter, UiBuilder.AccentColor, null, null, FontStyle.Bold);
            At(message, Center, Center, new Vector2(0f, 120f), new Vector2(1600f, 200f));

            // Pause menu
            var pauseDim = UiBuilder.CreateDim(canvas.transform, "PauseMenu");
            var pauseWindow = UiBuilder.CreatePanel(pauseDim.transform, "Window", UiBuilder.WindowColor, true);
            At(pauseWindow, Center, Center, Vector2.zero, new Vector2(760f, 820f));
            var pauseTitle = UiBuilder.CreateLabel(pauseWindow.transform, "Title", "pause.title", 60, TextAnchor.MiddleCenter, UiBuilder.AccentColor);
            At(pauseTitle, TopCenter, TopCenter, new Vector2(0f, -40f), new Vector2(600f, 80f));
            string[] pauseKeys = { "pause.resume", "pause.retry", "pause.settings", "pause.exit_map" };
            var pauseButtons = new List<Button>();
            for (int i = 0; i < pauseKeys.Length; i++)
            {
                var b = UiBuilder.CreateButton(pauseWindow.transform, "Button_" + pauseKeys[i].Substring(6), pauseKeys[i], new Vector2(560f, 100f), 44);
                At(b, TopCenter, TopCenter, new Vector2(0f, -160f - i * 130f), new Vector2(560f, 100f));
                pauseButtons.Add(b);
            }
            UiBuilder.LinkVertical(new List<Selectable>(pauseButtons), true);
            pauseDim.gameObject.SetActive(false);

            // Result panel
            var resultDim = UiBuilder.CreateDim(canvas.transform, "ResultPanel");
            var resultWindow = UiBuilder.CreatePanel(resultDim.transform, "Window", UiBuilder.WindowColor, true);
            At(resultWindow, Center, Center, Vector2.zero, new Vector2(900f, 560f));
            var resultTitle = UiBuilder.CreateLabel(resultWindow.transform, "Title", "ingame.clear", 80, TextAnchor.MiddleCenter, UiBuilder.AccentColor);
            At(resultTitle, TopCenter, TopCenter, new Vector2(0f, -50f), new Vector2(800f, 110f));
            var resultTime = UiBuilder.CreateLabel(resultWindow.transform, "TimeText", "ingame.result_time", 52, TextAnchor.MiddleCenter);
            At(resultTime, TopCenter, TopCenter, new Vector2(0f, -200f), new Vector2(800f, 80f));
            var resultContinue = UiBuilder.CreateButton(resultWindow.transform, "ContinueButton", "ingame.continue", new Vector2(480f, 100f), 46);
            At(resultContinue, BottomCenter, BottomCenter, new Vector2(0f, 60f), new Vector2(480f, 100f));
            UiBuilder.SetNav(resultContinue, null, null);
            resultDim.gameObject.SetActive(false);

            // Game over panel
            var overDim = UiBuilder.CreateDim(canvas.transform, "GameOverPanel");
            var overWindow = UiBuilder.CreatePanel(overDim.transform, "Window", UiBuilder.WindowColor, true);
            At(overWindow, Center, Center, Vector2.zero, new Vector2(900f, 640f));
            var overTitle = UiBuilder.CreateLabel(overWindow.transform, "Title", "ingame.gameover", 90, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.45f));
            At(overTitle, TopCenter, TopCenter, new Vector2(0f, -60f), new Vector2(800f, 130f));
            // Retry / World map (ContinueButton) / Title
            string[] overNames = { "RetryButton", "ContinueButton", "TitleButton" };
            string[] overKeys = { "pause.retry", "pause.exit_map", "pause.to_title" };
            var overButtons = new List<Button>();
            for (int i = 0; i < overNames.Length; i++)
            {
                var b = UiBuilder.CreateButton(overWindow.transform, overNames[i], overKeys[i], new Vector2(520f, 96f), 44);
                At(b, TopCenter, TopCenter, new Vector2(0f, -230f - i * 120f), new Vector2(520f, 96f));
                overButtons.Add(b);
            }
            UiBuilder.LinkVertical(new List<Selectable>(overButtons), true);
            overDim.gameObject.SetActive(false);

            var keeper = UiBuilder.CreateEventSystem(null, false);

            EditorWire.Set(view, "livesText", UiBuilder.Loc(lives));
            EditorWire.Set(view, "timeText", UiBuilder.Loc(time));
            EditorWire.Set(view, "stageNameText", UiBuilder.Loc(stageName));
            EditorWire.Set(view, "messageText", UiBuilder.Loc(message));
            EditorWire.Set(view, "coinsText", UiBuilder.Loc(coins));
            EditorWire.Set(view, "specialSlots", hudSlots);
            EditorWire.Set(view, "pausePanel", pauseDim.gameObject);
            EditorWire.Set(view, "resumeButton", pauseButtons[0]);
            EditorWire.Set(view, "retryButton", pauseButtons[1]);
            EditorWire.Set(view, "settingsButton", pauseButtons[2]);
            EditorWire.Set(view, "exitButton", pauseButtons[3]);
            EditorWire.Set(view, "resultPanel", resultDim.gameObject);
            EditorWire.Set(view, "resultTimeText", UiBuilder.Loc(resultTime));
            EditorWire.Set(view, "resultContinueButton", resultContinue);
            EditorWire.Set(view, "gameOverPanel", overDim.gameObject);
            EditorWire.Set(view, "gameOverRetryButton", overButtons[0]);
            EditorWire.Set(view, "gameOverContinueButton", overButtons[1]);
            EditorWire.Set(view, "gameOverTitleButton", overButtons[2]);
            EditorWire.Set(view, "focus", keeper);

            var ctrl = new GameObject("InGameController").AddComponent<InGameController>();
            EditorWire.Set(ctrl, "player", motor);
            EditorWire.Set(ctrl, "cameraFollow", follow);
            EditorWire.Set(ctrl, "view", view);
            EditorWire.Set(ctrl, "debugStageId", "W1_S1");

            SaveScene(scene, ScenesDir + "/06_InGame.unity");
        }

    }
}
