using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>World map flow (spec 5.5): node states, avatar movement, stage entry, pause menu.</summary>
    public sealed class WorldMapController : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private WorldMapNode[] nodes;
        [SerializeField] private WorldMapPath[] paths;
        [SerializeField] private MapPlayer mapPlayer;
        [SerializeField] private CameraFollow cameraFollow;

        [Header("Camera mode")]
        [Tooltip("Third-person camera facing along the stage order: Up/W = next stage, Down/S = previous, right mouse = look around. " +
                 "Off = classic top view where the arrow keys follow each node's fixed neighbours.")]
        [SerializeField] private bool thirdPersonCamera = true;
        [SerializeField] private WorldMapCameraRig cameraRig;

        [Header("Stage info panel")]
        [SerializeField] private LocalizedText stageNameText;
        [SerializeField] private LocalizedText stateText;
        [SerializeField] private LocalizedText bestTimeText;
        [SerializeField] private LocalizedText specialsText;
        [SerializeField] private Image[] specialSlots;
        [SerializeField] private LocalizedText messageText;
        [SerializeField] private Button enterButton;

        [Header("Pause menu")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button titleButton;
        [SerializeField] private Button quitButton;
        [Tooltip("Always-visible HUD button that opens the pause menu (mouse users).")]
        [SerializeField] private Button menuButton;
        [SerializeField] private UiFocusKeeper focus;

        [System.Serializable]
        public struct WorldBgm
        {
            [Tooltip("Node ids starting with this prefix use the clip (e.g. \"W1\"; longest prefix wins, so \"W1_B\" can override).")]
            public string nodeIdPrefix;
            public AudioClip clip;
        }

        [Tooltip("Fallback looping world map BGM for nodes no world entry covers.")]
        [SerializeField] private AudioClip bgm;
        [Tooltip("Per-world BGM, switched when the avatar arrives at a node of another world.")]
        [SerializeField] private WorldBgm[] worldBgms = new WorldBgm[0];
        [Tooltip("Optional. When set, the BGM follows the world terrain under the avatar instead of the current node.")]
        [SerializeField] private WorldMapGroundRegion groundRegion;

        [SerializeField, Range(0.3f, 0.9f)] private float directionThreshold = 0.6f;
        [SerializeField, Min(0f)] private float messageSeconds = 2.5f;

        [Header("Zoom (mouse wheel, -/= keys, gamepad shoulders)")]
        [Tooltip("Zoom multiplier per wheel notch toward the player (smaller = stronger zoom per notch).")]
        [SerializeField, Range(0.5f, 0.98f)] private float zoomStepFactor = 0.85f;
        [Tooltip("Wheel-notch equivalents per second while a zoom key is held.")]
        [SerializeField, Min(0f)] private float zoomHeldStepsPerSecond = 4f;

        [Header("Master clear (first time only)")]
        [Tooltip("Burst, pad reveal and banner shown on the first return after a stage becomes mastered; the unlock notice follows.")]
        [SerializeField] private WorldMapMasterPresentation.Settings masterPresentationSettings = new WorldMapMasterPresentation.Settings();

        [Header("Mouse")]
        [Tooltip("Click radius around a node centre (m); clicking the node you stand on enters it.")]
        [SerializeField, Min(0.2f)] private float nodePickRadius = 1.25f;
        private Camera mapCamera;
        /// <summary>Node ids in stage order (the chain of paths from the start node).</summary>
        private readonly List<string> order = new List<string>();
        private bool ThirdPerson => thirdPersonCamera && cameraRig != null;
        private WorldMapLabelOverlay labelOverlay;

        private readonly Dictionary<string, WorldMapNode> nodeById = new Dictionary<string, WorldMapNode>();
        private GameSystems systems;
        private string currentNodeId;
        private bool paused;
        private WorldMapPresentation presentation;
        private StageUnlockNotice unlockNotice;
        private StageLockedNotice lockedNotice;
        private WorldMapMasterPresentation masterPresentation;
        /// <summary>Node that became mastered by the clear that brought us here; shown cleared until its reveal.</summary>
        private string pendingMasterNodeId;
        private bool unlocksPresented;
        private bool outroPlaying;
        private bool presentingVoiceSlots;
        private VoiceSlotUnlockNotice voiceNotice;
        private MapDirection lastDirection = MapDirection.None;
        private float messageHideAt;

        private WorldMapNode CurrentNode => nodeById[currentNodeId];

        private void Awake()
        {
            Assert.IsNotNull(nodes);
            Assert.IsTrue(nodes.Length > 0, "WorldMapController has no nodes.");
            Assert.IsNotNull(paths);
            Assert.IsNotNull(mapPlayer);
            Assert.IsNotNull(cameraFollow);
            Assert.IsNotNull(stageNameText);
            Assert.IsNotNull(stateText);
            Assert.IsNotNull(bestTimeText);
            Assert.IsNotNull(messageText);
            Assert.IsNotNull(specialsText);
            Assert.IsNotNull(specialSlots);
            Assert.IsNotNull(enterButton);
            Assert.IsNotNull(pausePanel);
            Assert.IsNotNull(resumeButton);
            Assert.IsNotNull(settingsButton);
            Assert.IsNotNull(saveButton);
            Assert.IsNotNull(loadButton);
            Assert.IsNotNull(titleButton);
            Assert.IsNotNull(quitButton);
            Assert.IsNotNull(menuButton);
            Assert.IsNotNull(focus);

            presentation = WorldMapPresentation.Apply(menuButton.transform.parent);
            presentation.Configure(focus);
            enterButton.onClick.AddListener(TryEnter);
            resumeButton.onClick.AddListener(Resume);
            settingsButton.onClick.AddListener(OpenSettings);
            saveButton.onClick.AddListener(SaveGame);
            loadButton.onClick.AddListener(LoadGame);
            titleButton.onClick.AddListener(BackToTitle);
            quitButton.onClick.AddListener(QuitGame);
            menuButton.onClick.AddListener(Pause);
            if (presentation.StudioHudButton != null)
                presentation.StudioHudButton.onClick.AddListener(OpenVoiceStudio);
            if (presentation.StudioMenuButton != null)
                presentation.StudioMenuButton.onClick.AddListener(OpenVoiceStudio);
            if (presentation.RankingHudButton != null)
                presentation.RankingHudButton.onClick.AddListener(OpenRanking);
            if (presentation.RankingMenuButton != null)
                presentation.RankingMenuButton.onClick.AddListener(OpenRanking);
            mapPlayer.Arrived += OnArrived;
            if (groundRegion != null)
            {
                groundRegion.SetTarget(mapPlayer.transform);
                groundRegion.RegionChanged += OnGroundRegionChanged;
            }
            pausePanel.SetActive(false);
            messageText.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (mapPlayer != null)
                mapPlayer.Arrived -= OnArrived;
            if (groundRegion != null)
                groundRegion.RegionChanged -= OnGroundRegionChanged;
            Time.timeScale = 1f;
        }

        private IEnumerator Start()
        {
            systems = GameSystems.Instance;
            var save = systems.Save;
            if (!save.HasSave)
                save.CreateTemporary(systems.StageDb.StartNodeId); // direct play (spec 4.3)

            foreach (var n in nodes)
                nodeById[n.Data.NodeId] = n;

            var request = systems.Loader.LastRequest;
            outroPlaying = request != null && request.Reason == LoadReason.StageClear && request.FirstClear
                && systems.StageDb.TryGetStage(request.StageId, out var clearedStage) && clearedStage.PlayOutroOnFirstClear;
            pendingMasterNodeId = ResolveNewlyMasteredNode(request, save.Current);
            RefreshStates();

            currentNodeId = save.Current.currentNodeId;
            if (!nodeById.ContainsKey(currentNodeId) || CurrentNode.State == NodeState.Locked)
            {
                Debug.LogWarning($"[WorldMap] currentNodeId '{currentNodeId}' is invalid or locked. Using start node.");
                currentNodeId = systems.StageDb.StartNodeId;
                Assert.IsTrue(nodeById.ContainsKey(currentNodeId), "Start node is missing from the WorldMap scene.");
            }

            mapPlayer.Teleport(CurrentNode.Anchor);
            if (groundRegion != null)
                groundRegion.Snap();
            BuildOrder();
            presentation.SetEncounterStages(order.ConvertAll(id => nodeById[id].Data.Stage.StageId));
            SetupCamera();

            focus.SetRestore(false);
            focus.ClearSelection();
            ShowNodeInfo();
            if (outroPlaying)
            {
                // Let the map finish its loading fade before the film takes over.
                while (systems.Loader.IsBusy || systems.Popups.IsBlocking)
                    yield return null;
                systems.InputMode.SetMode(InputMode.UI);
                var film = IntroSequencePlayer.PlayOutro(systems);
                while (film != null && !film.IsFinished)
                    yield return null;
                if (film != null)
                    Destroy(film.gameObject);
                focus.ClearSelection();
                outroPlaying = false;
            }
            systems.InputMode.SetMode(InputMode.Gameplay);
            PlayWorldBgm();
            if (request != null && request.Reason == LoadReason.StageClear)
            {
                if (pendingMasterNodeId != null) PresentMaster(request);
                else PresentUnlocks(request);
            }
            StartCoroutine(AnnounceVoiceSlots());
            WorldMapTutorialDirector.Attach(this, menuButton.transform.parent);
        }

        // ------------------------------------------------------------------ voice studio

        private IEnumerator AnnounceVoiceSlots()
        {
            if (!VoiceRecorder.Supported)
                yield break; // no Voice Studio on this platform: nothing to announce
            var save = systems.Save.Current;
            var progress = save.Voice;
            for (int i = 0; i < VoiceStudioConfig.SlotCount; i++)
            {
                var slot = (VoiceSlot)i;
                if (progress.announced[i] || !Voice.IsUnlocked(slot, save))
                    continue;
                presentingVoiceSlots = true;
                while (systems.Loader.IsBusy || systems.Popups.IsBlocking || MasterPlaying || (unlockNotice != null && unlockNotice.IsPlaying))
                    yield return null;
                if (voiceNotice == null)
                    voiceNotice = VoiceSlotUnlockNotice.Create(menuButton.GetComponentInParent<Canvas>().transform);
                yield return voiceNotice.Show(slot);
                progress.announced[i] = true;
                if (!systems.Save.Save())
                    Debug.LogError("[WorldMap] Failed to save the voice slot notice state.");
            }
            presentingVoiceSlots = false;
        }

        /// <summary>Opens the Voice Studio from the HUD button (map view) or the pause menu.</summary>
        private void OpenVoiceStudio()
        {
            if (!CanUseMenu || TutorialHold || (lockedNotice != null && lockedNotice.IsPlaying))
                return;
            focus.SetRestore(true); // keyboard / gamepad focus must survive a stray mouse click inside the studio
            systems.Popups.OpenVoiceStudio(() =>
            {
                if (paused)
                    return; // back in the pause menu, which keeps its own focus
                focus.SetRestore(false);
                focus.ClearSelection();
                lastDirection = MapDirection.Right; // swallow a held direction
            });
        }

        /// <summary>Opens the rankings from the HUD button (map view) or the pause menu, starting at the current node's stage.</summary>
        private void OpenRanking()
        {
            if (!CanUseMenu || TutorialHold || (lockedNotice != null && lockedNotice.IsPlaying))
                return;
            string stageId = nodeById.TryGetValue(currentNodeId ?? "", out var node) && node.Data.Stage != null ? node.Data.Stage.StageId : null;
            focus.SetRestore(true);
            systems.Popups.OpenRanking(stageId, () =>
            {
                if (paused)
                    return; // back in the pause menu, which keeps its own focus
                focus.SetRestore(false);
                focus.ClearSelection();
                lastDirection = MapDirection.Right; // swallow a held direction
            });
        }

        // ------------------------------------------------------------------ tutorial hooks (read-only queries + events)

        /// <summary>The avatar started walking to another node (key, stick or click).</summary>
        public event System.Action NodeMoveStarted;
        /// <summary>A stage load was requested from the map.</summary>
        public event System.Action StageEntering;

        /// <summary>Something else owns the screen or the avatar is walking: no tutorial prompt.</summary>
        public bool IsTutorialBlocked => systems == null || outroPlaying || presentingVoiceSlots || paused || MasterPlaying || (unlockNotice != null && unlockNotice.IsPlaying)
            || (lockedNotice != null && lockedNotice.IsPlaying) || systems.Loader.IsBusy || systems.Popups.IsBlocking || mapPlayer.IsMoving;

        /// <summary>The current node can be entered (open and its scene exists).</summary>
        public bool CanEnterCurrentNode => systems != null && nodeById.ContainsKey(currentNodeId ?? "") && CurrentNode.State != NodeState.Locked
            && Application.CanStreamedLevelBeLoaded(CurrentNode.Data.Stage.SceneName);

        /// <summary>A neighbouring node in stage order is open (so next / previous does something).</summary>
        public bool CanStepToOtherNode
        {
            get
            {
                if (systems == null || currentNodeId == null)
                    return false;
                int i = order.IndexOf(currentNodeId);
                foreach (int k in new[] { i + 1, i - 1 })
                    if (i >= 0 && k >= 0 && k < order.Count && nodeById.TryGetValue(order[k], out var n) && n.State != NodeState.Locked)
                        return true;
                return false;
            }
        }

        /// <summary>Set while a tutorial explanation is up: the map ignores all input (the overlay also blocks clicks).</summary>
        public bool TutorialHold { get; set; }

        public Camera MapCamera
        {
            get
            {
                if (mapCamera == null && cameraFollow != null)
                    mapCamera = cameraFollow.GetComponent<Camera>();
                return mapCamera;
            }
        }

        /// <summary>World position of the pad the avatar stands on.</summary>
        public Vector3 CurrentNodeAnchor => currentNodeId != null && nodeById.ContainsKey(currentNodeId) ? CurrentNode.Anchor : Vector3.zero;

        /// <summary>World position of the open neighbour in stage order (next first, then previous).</summary>
        public bool TryGetOpenNeighbourAnchor(out Vector3 anchor)
        {
            anchor = Vector3.zero;
            if (currentNodeId == null)
                return false;
            int i = order.IndexOf(currentNodeId);
            foreach (int k in new[] { i + 1, i - 1 })
                if (i >= 0 && k >= 0 && k < order.Count && nodeById.TryGetValue(order[k], out var n) && n.State != NodeState.Locked)
                {
                    anchor = n.Anchor;
                    return true;
                }
            return false;
        }

        /// <summary>The bottom stage-info panel (the prompt sits above it).</summary>
        public RectTransform InfoPanel => menuButton != null ? menuButton.transform.parent.Find("StageInfoPanel") as RectTransform : null;

        private void RefreshStates()
        {
            var save = systems.Save.Current;
            foreach (var n in nodes)
                n.Apply(NodeStateResolver.Resolve(n.Data, save), IsMastered(n.Data, save) && n.Data.NodeId != pendingMasterNodeId);

            foreach (var p in paths)
            {
                bool visible = nodeById.TryGetValue(p.FromNodeId, out var a)
                    && nodeById.TryGetValue(p.ToNodeId, out var b)
                    && a.State != NodeState.Locked
                    && b.State != NodeState.Locked;
                p.SetVisible(visible);
            }
        }

        /// <summary>Master clear: the stage is cleared and every collectible of it has been found.</summary>
        private static bool IsMastered(WorldMapNodeData node, SaveData save) => StageMastery.IsMastered(node.Stage, save);

        /// <summary>
        /// Node of the stage that the clear bringing us here newly mastered (flag computed by the clear routine from the
        /// save before/after), or null. The save must still agree, so a stale request never replays the presentation.
        /// </summary>
        private string ResolveNewlyMasteredNode(LoadRequest request, SaveData save)
        {
            if (request == null || request.Reason != LoadReason.StageClear || !request.NewlyMastered || string.IsNullOrEmpty(request.StageId))
                return null;
            if (!systems.StageDb.TryGetNodeByStage(request.StageId, out var data) || !nodeById.ContainsKey(data.NodeId))
                return null;
            return IsMastered(data, save) && NodeStateResolver.Resolve(data, save) == NodeState.Cleared ? data.NodeId : null;
        }

        /// <summary>Master burst + gold pad + banner, then the regular unlock presentation.</summary>
        private void PresentMaster(LoadRequest request)
        {
            var node = nodeById[pendingMasterNodeId];
            pendingMasterNodeId = null;
            if (masterPresentation == null)
                masterPresentation = WorldMapMasterPresentation.Create(transform, menuButton.GetComponentInParent<Canvas>().transform);
            masterPresentation.Play(node, masterPresentationSettings, () => PresentUnlocks(request));
        }

        private bool MasterPlaying => masterPresentation != null && masterPresentation.IsPlaying;

        /// <summary>Highlights nodes that opened because of the cleared stage.</summary>
        private void PresentUnlocks(LoadRequest request)
        {
            if(unlocksPresented || request.Reason!=LoadReason.StageClear)return;
            unlocksPresented=true;
            var opened=new List<WorldMapNodeData>();
            foreach(var id in request.NewlyUnlockedNodeIds)
            {
                if(string.IsNullOrEmpty(id)||!nodeById.TryGetValue(id,out var node)||node.State!=NodeState.Open)continue;
                node.PlayUnlockAttention(7f);opened.Add(node.Data);
            }
            if(opened.Count==0)return;
            unlockNotice=StageUnlockNotice.Create(menuButton.GetComponentInParent<Canvas>().transform);
            unlockNotice.Show(opened);
        }

        private void Update()
        {
            if (outroPlaying)
                return;
            if(lockedNotice!=null&&(paused||systems.Loader.IsBusy||systems.Popups.IsBlocking||MasterPlaying||(unlockNotice!=null&&unlockNotice.IsPlaying)))lockedNotice.Hide();
            if(masterPresentation!=null)masterPresentation.Suspended=systems.Loader.IsBusy||systems.Popups.IsBlocking||paused;
            if(unlockNotice!=null)unlockNotice.Suspended=systems.Loader.IsBusy||systems.Popups.IsBlocking||paused;
            if (systems.Loader.IsBusy)
                return;

            if (messageText.gameObject.activeSelf && Time.unscaledTime >= messageHideAt)
                messageText.gameObject.SetActive(false);

            if (TutorialHold)
                return; // the explanation bubble reads the input meanwhile

            var input = systems.InputMode;
            if(MasterPlaying)
            {
                if(input.ConfirmPressed||input.CancelPressed||input.PausePressed||input.PointerClicked)masterPresentation.Skip();
                return;
            }
            if(unlockNotice!=null&&unlockNotice.IsPlaying)
            {
                if(!unlockNotice.Suspended&&(input.ConfirmPressed||input.CancelPressed||input.PausePressed))unlockNotice.Dismiss();
                return;
            }
            if (presentingVoiceSlots)
            {
                if (!systems.Popups.IsBlocking && voiceNotice != null && (input.ConfirmPressed || input.CancelPressed || input.PausePressed))
                    voiceNotice.Dismiss();
                return;
            }
            if (ThirdPerson) // LookHeld is false outside gameplay (pause menu, popups), which releases the free look
                cameraRig.Look(input.LookHeld && !paused && !systems.Popups.IsBlocking, input.LookDelta);
            if (systems.Popups.IsBlocking)
                return;

            if (paused)
            {
                if (input.PausePressed || input.CancelPressed)
                {
                    Resume();
                }
                return;
            }

            if (input.PausePressed)
            {
                Pause();
                return;
            }
            UpdateZoom(input);
            if (mapPlayer.IsMoving)
                return;

            if (input.PointerClicked && !IsPointerOverUi())
            {
                var clicked = PickNode(input.PointerPosition);
                if (clicked != null)
                {
                    GoToNode(clicked);
                    return;
                }
            }

            var dir = ReadDirection(input.Move);
            if (dir != MapDirection.None)
            {
                if (ThirdPerson)
                    StepAlongOrder(dir);
                else
                    TryMove(dir);
            }
            if (input.ConfirmPressed)
                TryEnter();
        }

        private MapDirection ReadDirection(Vector2 move)
        {
            MapDirection dir = MapDirection.None;
            if (move.magnitude >= directionThreshold)
            {
                if (Mathf.Abs(move.x) >= Mathf.Abs(move.y))
                    dir = move.x > 0f ? MapDirection.Right : MapDirection.Left;
                else
                    dir = move.y > 0f ? MapDirection.Up : MapDirection.Down;
            }
            // Edge trigger: one step per press; must release before the next one.
            if (dir == lastDirection)
                return MapDirection.None;
            lastDirection = dir;
            return dir;
        }

        private void TryMove(MapDirection dir)
        {
            string neighborId = CurrentNode.Data.GetNeighborId(dir);
            if (string.IsNullOrEmpty(neighborId))
                return;
            if (!nodeById.TryGetValue(neighborId, out var next))
            {
                Debug.LogError($"[WorldMap] Neighbour '{neighborId}' of '{currentNodeId}' is missing in the scene.");
                return;
            }
            if (next.State == NodeState.Locked)
            {
                ShowLocked(next);
                return;
            }
            var route = FindRoute(currentNodeId, neighborId);
            if(lockedNotice!=null)lockedNotice.Hide();
            currentNodeId = neighborId;
            messageText.gameObject.SetActive(false);
            if (route.Length > 0)
                mapPlayer.MoveAlong(route);
            else
                mapPlayer.MoveTo(next.Anchor);
            ShowNodeInfo();
            NodeMoveStarted?.Invoke();
        }

        // ------------------------------------------------------------------ mouse

        private static bool IsPointerOverUi() =>
            UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        /// <summary>Node whose pad is under the screen point (nearest centre within the pick radius), or null.</summary>
        private WorldMapNode PickNode(Vector2 screenPoint)
        {
            if (mapCamera == null)
                mapCamera = cameraFollow.GetComponent<Camera>();
            if (mapCamera == null)
                return null;
            WorldMapNode best = null;
            float bestDist = float.MaxValue;
            foreach (var n in nodes)
            {
                if (n == null)
                    continue;
                Vector3 centre = mapCamera.WorldToScreenPoint(n.Anchor);
                if (centre.z <= 0f)
                    continue;
                Vector3 edge = mapCamera.WorldToScreenPoint(n.Anchor + mapCamera.transform.right * nodePickRadius);
                float radius = Vector2.Distance(centre, edge);
                float d = Vector2.Distance(screenPoint, centre);
                if (d <= radius && d < bestDist)
                {
                    best = n;
                    bestDist = d;
                }
            }
            return best;
        }

        /// <summary>
        /// Clicked node: enter it when standing on it, otherwise walk there along the open paths (several nodes at once).
        /// </summary>
        private void GoToNode(WorldMapNode target)
        {
            string targetId = target.Data.NodeId;
            if (targetId == currentNodeId)
            {
                TryEnter();
                return;
            }
            if (target.State == NodeState.Locked)
            {
                ShowLocked(target);
                return;
            }
            var hops = FindNodeChain(currentNodeId, targetId);
            if (hops == null)
            {
                ShowLocked(target,true);
                return;
            }

            var route = new List<Vector3>();
            for (int i = 1; i < hops.Count; i++)
            {
                var leg = FindRoute(hops[i - 1], hops[i]);
                if (leg.Length > 0)
                    route.AddRange(leg);
                else
                    route.Add(nodeById[hops[i]].Anchor);
            }
            if(lockedNotice!=null)lockedNotice.Hide();
            currentNodeId = targetId;
            messageText.gameObject.SetActive(false);
            mapPlayer.MoveAlong(route.ToArray());
            ShowNodeInfo();
            NodeMoveStarted?.Invoke();
        }

        /// <summary>Shortest node chain (breadth-first over paths whose both ends are unlocked), or null when unreachable.</summary>
        private List<string> FindNodeChain(string fromId, string toId)
        {
            var previous = new Dictionary<string, string> { [fromId] = null };
            var queue = new Queue<string>();
            queue.Enqueue(fromId);
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                if (at == toId)
                    break;
                foreach (var p in paths)
                {
                    if (p == null)
                        continue;
                    string next = p.FromNodeId == at ? p.ToNodeId : p.ToNodeId == at ? p.FromNodeId : null;
                    if (next == null || previous.ContainsKey(next)
                        || !nodeById.TryGetValue(next, out var node) || node.State == NodeState.Locked)
                        continue;
                    previous[next] = at;
                    queue.Enqueue(next);
                }
            }
            if (!previous.ContainsKey(toId))
                return null;
            var chain = new List<string>();
            for (string id = toId; id != null; id = previous[id])
                chain.Add(id);
            chain.Reverse();
            return chain;
        }

        /// <summary>Route of the path joining the two nodes, in travel order. Empty (straight move) when there is none.</summary>
        private Vector3[] FindRoute(string fromId, string toId)
        {
            foreach (var p in paths)
            {
                if (p != null && p.Connects(fromId, toId))
                    return p.GetRoute(fromId);
            }
            Debug.LogError($"[WorldMap] No path joins '{fromId}' and '{toId}'; moving in a straight line.");
            return new Vector3[0];
        }

        private void OnArrived()
        {
            // Save the current node when the avatar stops (spec 6 "node move").
            systems.Save.Current.currentNodeId = currentNodeId;
            if (!systems.Save.Save())
                Debug.LogError("[WorldMap] Failed to save current node.");
            PlayWorldBgm();
            FaceCamera();
            ShowNodeInfo();
        }

        // ------------------------------------------------------------------ third-person camera

        /// <summary>Stage order: walk the path chain from the start node (the layout is one linear chain).</summary>
        private void BuildOrder()
        {
            order.Clear();
            var visited = new HashSet<string>();
            string at = systems.StageDb.StartNodeId;
            while (at != null && visited.Add(at) && nodeById.ContainsKey(at))
            {
                order.Add(at);
                string next = null;
                foreach (var p in paths)
                {
                    if (p == null)
                        continue;
                    string other = p.FromNodeId == at ? p.ToNodeId : p.ToNodeId == at ? p.FromNodeId : null;
                    if (other != null && !visited.Contains(other))
                    {
                        next = other;
                        break;
                    }
                }
                at = next;
            }
            if (order.Count != nodes.Length)
                Debug.LogWarning($"[WorldMap] Stage order covers {order.Count} of {nodes.Length} nodes (the paths are not one chain).");
        }

        private void SetupCamera()
        {
            if (!ThirdPerson)
            {
                if (cameraRig != null)
                    cameraRig.enabled = false;
                cameraFollow.enabled = true;
                cameraFollow.SetTarget(mapPlayer.transform);
                cameraFollow.SnapToTarget();
                SetupLabels(cameraFollow.GetComponent<Camera>());
                return;
            }
            cameraFollow.enabled = false;
            cameraRig.enabled = true;

            var line = new List<Vector3>();
            for (int i = 0; i < order.Count; i++)
            {
                if (i == 0)
                {
                    line.Add(nodeById[order[0]].Anchor);
                    continue;
                }
                var leg = FindRoute(order[i - 1], order[i]);
                if (leg.Length > 0)
                    line.AddRange(leg);
                else
                    line.Add(nodeById[order[i]].Anchor);
            }
            cameraRig.SetTarget(mapPlayer.transform);
            cameraRig.SetProgression(line, CurrentNode.Anchor);
            cameraRig.SnapToTarget();
            SetupLabels(cameraRig.GetComponent<Camera>());
            FaceCamera();
        }

        /// <summary>Node ids become crisp screen-space badges (both camera modes); built once per scene.</summary>
        private void SetupLabels(Camera cam)
        {
            if (labelOverlay != null)
                return;
            labelOverlay = new GameObject("NodeLabels").AddComponent<WorldMapLabelOverlay>();
            labelOverlay.Build(cam, nodes);
        }

        /// <summary>The standing avatar turns toward the camera (its yaw changes with the path heading).</summary>
        private void FaceCamera()
        {
            if (ThirdPerson)
                mapPlayer.SetIdleYaw(cameraRig.Yaw + 180f);
        }

        /// <summary>Up = next stage in order, Down = previous; left/right do nothing in the third-person view.</summary>
        private void StepAlongOrder(MapDirection dir)
        {
            int step = dir == MapDirection.Up ? 1 : dir == MapDirection.Down ? -1 : 0;
            if (step == 0)
                return;
            int i = order.IndexOf(currentNodeId) + step;
            if (i < 0 || i >= order.Count)
                return;
            GoToNode(nodeById[order[i]]);
        }

        private void OnGroundRegionChanged(string prefix)
        {
            if (systems != null && !outroPlaying)
                PlayWorldBgm();
        }

        /// <summary>
        /// Plays the world BGM for the terrain under the avatar (or the current node when no region terrain is known).
        /// AudioService keeps an already-playing clip going.
        /// </summary>
        private void PlayWorldBgm()
        {
            string regionId = groundRegion != null && groundRegion.CurrentPrefix != null ? groundRegion.CurrentPrefix : currentNodeId;
            AudioClip clip = bgm;
            int bestLength = -1;
            foreach (var w in worldBgms)
            {
                if (w.clip == null || string.IsNullOrEmpty(w.nodeIdPrefix))
                    continue;
                if (regionId.StartsWith(w.nodeIdPrefix, System.StringComparison.Ordinal) && w.nodeIdPrefix.Length > bestLength)
                {
                    clip = w.clip;
                    bestLength = w.nodeIdPrefix.Length;
                }
            }
            if (clip != null)
                systems.Audio.PlayBgm(clip);
        }

        private void ShowNodeInfo()
        {
            var node = CurrentNode;
            var stage = node.Data.Stage;
            presentation.SetRoutePosition(order.IndexOf(currentNodeId), order.Count);
            stageNameText.SetKey(stage.DisplayNameKey);
            switch (node.State)
            {
                case NodeState.Cleared: stateText.SetKey("worldmap.state.cleared"); break;
                case NodeState.Open: stateText.SetKey("worldmap.state.open"); break;
                default: stateText.SetKey("worldmap.state.locked"); break;
            }

            var record = systems.Save.Current.FindStage(stage.StageId);
            if (record != null && record.cleared && record.bestTime > 0f)
                bestTimeText.SetText(TimeFormat.Format(record.bestTime, true));
            else
                bestTimeText.SetText("—");
            int collected = record != null ? record.CollectedCount() : 0;
            specialsText.SetText($"{collected} / {stage.CollectibleCount}");
            for (int i = 0; i < specialSlots.Length; i++)
                CollectibleSlot.Set(specialSlots[i],record != null && record.IsCollected(i));
            enterButton.interactable = node.State != NodeState.Locked && !mapPlayer.IsMoving;
        }

        private void ShowLocked(WorldMapNode target,bool disconnected=false)
        {
            messageText.gameObject.SetActive(false);
            if(lockedNotice==null)lockedNotice=StageLockedNotice.Create(menuButton.GetComponentInParent<Canvas>().transform);
            lockedNotice.Show(target.Data,systems.Save.Current,disconnected);
        }

        private void ShowMessage(string key)
        {
            if(lockedNotice!=null)lockedNotice.Hide();
            messageText.gameObject.SetActive(true);
            messageText.SetKey(key);
            messageHideAt = Time.unscaledTime + messageSeconds;
        }

        private void TryEnter()
        {
            if (outroPlaying || presentingVoiceSlots || MasterPlaying || (unlockNotice!=null&&unlockNotice.IsPlaying) || paused || mapPlayer.IsMoving || systems.Loader.IsBusy || systems.Popups.IsBlocking)
                return;
            var node = CurrentNode;
            if (node.State == NodeState.Locked)
            {
                ShowLocked(node);
                return;
            }
            // Nodes of the full world map exist before their stage scenes are made.
            if (!Application.CanStreamedLevelBeLoaded(node.Data.Stage.SceneName))
            {
                ShowMessage("worldmap.coming_soon");
                return;
            }
            focus.ClearSelection();
            StageEntering?.Invoke();
            systems.Loader.LoadWithLoading(new LoadRequest(SceneId.InGame, node.Data.Stage.StageId, LoadReason.StageEnter));
        }

        private void UpdateZoom(InputModeService input)
        {
            float steps = input.ZoomWheelSteps + input.ZoomHeld * zoomHeldStepsPerSecond * Time.unscaledDeltaTime;
            if (steps == 0f)
                return;
            if (ThirdPerson)
                cameraRig.ZoomBy(Mathf.Pow(zoomStepFactor, steps));
            else
                cameraFollow.ZoomBy(Mathf.Pow(zoomStepFactor, steps));
        }

        private bool CanUseMenu => systems != null && !outroPlaying && !presentingVoiceSlots && !systems.Loader.IsBusy && !systems.Popups.IsBlocking && !MasterPlaying && (unlockNotice==null||!unlockNotice.IsPlaying);

        /// <summary>Writes the progress now (it is also saved automatically whenever the avatar stops on a node).</summary>
        private void SaveGame()
        {
            if (!CanUseMenu)
                return;
            var save = systems.Save;
            save.Current.currentNodeId = currentNodeId;
            if (save.IsTemporary)
            {
                ShowMessage("worldmap.save_temporary"); // direct scene play: nothing is written to disk
                return;
            }
            ShowMessage(save.Save() ? "worldmap.saved" : "worldmap.save_failed");
        }

        /// <summary>Asks, then reloads the progress from the save file and re-opens the world map from it.</summary>
        private void LoadGame()
        {
            if (!CanUseMenu)
                return;
            if (!systems.Save.HasSaveOnDisk)
            {
                ShowMessage("worldmap.no_save");
                return;
            }
            systems.Popups.OpenConfirm("worldmap.confirm_load", ReloadSave);
        }

        private void ReloadSave()
        {
            if (!systems.Save.ReloadFromDisk())
            {
                ShowMessage("worldmap.load_failed");
                return;
            }
            focus.ClearSelection();
            systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.Continue));
        }

        /// <summary>Asks, then saves and quits the application.</summary>
        private void QuitGame()
        {
            if (!CanUseMenu)
                return;
            systems.Popups.OpenConfirm("worldmap.confirm_quit", () =>
            {
                systems.Save.Current.currentNodeId = currentNodeId;
                if (!systems.Save.Save())
                    Debug.LogError("[WorldMap] Failed to save before quitting.");
                AppQuit.Quit();
            });
        }

        private void Pause()
        {
            if (paused || !CanUseMenu)
                return;
            paused = true;
            presentation.SetMenuVisible(true);
            pausePanel.SetActive(true);
            systems.InputMode.SetMode(InputMode.UI);
            focus.SetRestore(true);
            focus.Select(resumeButton.gameObject);
        }

        private void Resume()
        {
            if (!paused)
                return;
            paused = false;
            pausePanel.SetActive(false);
            presentation.SetMenuVisible(false);
            systems.InputMode.SetMode(InputMode.Gameplay);
            focus.SetRestore(false);
            focus.ClearSelection();
            lastDirection = MapDirection.Right; // swallow a held direction
        }

        private void OpenSettings()
        {
            if (systems.Loader.IsBusy || systems.Popups.IsBlocking)
                return;
            systems.Popups.OpenSettings();
        }

        private void BackToTitle()
        {
            if (systems.Loader.IsBusy || systems.Popups.IsBlocking)
                return;
            if (!systems.Save.Save())
                Debug.LogError("[WorldMap] Failed to save before returning to the title.");
            systems.Loader.Load(SceneId.Title);
        }
    }
}
