using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>
    /// Voice Studio (world map): the player records their own voice for jump / dodge / perfect dodge / defeat / "Now!",
    /// and late in the game the stage-clear cheer, the boss victory line and the title call.
    /// Left: the slots (opened by the golden-leaf total). Right: the selected slot with its takes (extra takes are
    /// bought with coins and play at random), the waveform, and Record / Listen / Delete / Use.
    /// Recording: 3-2-1 countdown, then the microphone runs up to the slot's time limit; the take is trimmed and levelled
    /// (VoiceRecorder.Clean), saved (VoiceLibrary) and played back once. Built at runtime; no prefab.
    /// </summary>
    public sealed class VoiceStudioPopup : PopupBase
    {
        private enum Mode { Idle, Countdown, Recording }

        private const float Width = 1560f, Height = 950f, Margin = 52f, ListWidth = 390f, PanelX = 510f;
        private const float PanelWidth = Width - PanelX - Margin;
        private static readonly Color Locked = new Color(.64f, .63f, .59f);
        private static readonly Color Good = new Color(1f, .82f, .45f);
        private static readonly Color Surface = new Color(.085f, .088f, .091f);
        private static readonly Color Rule = new Color(.58f, .47f, .29f, .50f);

        private GameSystems systems;
        private VoiceStudioConfig config;
        private SaveData save;
        private VoiceProgress progress;
        private Action onClosed;
        private InputMode previousMode;

        private RectTransform window, waveBox;
        private LocalizedText title, wallet, slotTitle, slotDesc, status, countdownText, listTitle, openCount, monitorTitle, duration;
        private RectTransform slotMarker, takeMarker, progressRail, progressFill;
        private Button[] slotButtons, takeButtons;
        private LocalizedText[] slotStatus;
        private RectTransform[] lockBadges;
        private Button recordButton, playButton, deleteButton, useButton, closeButton;
        private VoiceWaveGraphic wave;
        private VoiceSlotGlyph[] slotGlyphs;
        private VoiceSlotGlyph monitorGlyph;
        private GoldUiInlay illustration;
        private float revealTime;

        private readonly VoiceRecorder recorder = new VoiceRecorder();
        private Mode mode = Mode.Idle;
        private int slotIndex, takeIndex;
        private float countdown;
        private bool previewing, built, cleaned;
        private string statusKey;
        private object[] statusArgs;
        private bool statusWarning;

        public static VoiceStudioPopup Create(Transform parent)
        {
            var rt = GoldUiTheme.Rect("VoiceStudioPopup", parent);
            GoldUiTheme.Stretch(rt);
            rt.gameObject.AddComponent<Image>().color = GoldUiTheme.Dim; // blocks clicks on the map behind
            rt.gameObject.AddComponent<CanvasGroup>();
            return rt.gameObject.AddComponent<VoiceStudioPopup>();
        }

        public void Open(PopupHost host, Action closedCallback)
        {
            systems = GameSystems.Instance;
            config = VoiceStudioConfig.Load();
            save = systems.Save.Current;
            progress = save.Voice;
            onClosed = closedCallback;
            previousMode = systems.InputMode.Mode;
            systems.InputMode.SetMode(InputMode.UI); // the map must not read Confirm / Move while the studio is open

            Build();
            // Start on the first open slot that has no recording yet (what the player most likely came for).
            slotIndex = 0;
            for (int i = VoiceStudioConfig.SlotCount - 1; i >= 0; i--)
                if (Unlocked(i) && !AnyRecording(i)) slotIndex = i;
            BeginOpen(host, slotButtons[slotIndex].gameObject);
            Layout(); // after the theme pass of BeginOpen, which restyles every text and button
            SelectSlot(slotIndex);
        }

        // ------------------------------------------------------------------ build / layout

        private void Build()
        {
            if (built)
                return;
            built = true;
            window = GoldUiTheme.Rect("Window", transform);
            window.gameObject.AddComponent<Image>().color = GoldUiTheme.Panel;
            GoldUiInlay.Add(window, GoldUiInlay.Surface.Window);
            var studioSeal = Glyph(window, VoiceSlot.TitleCall, Margin, -27, 54);
            studioSeal.color = Good;
            title = UiKit.Label(window, "Title", "voice.title", UiKit.HeadingSize, TextAnchor.MiddleCenter);
            wallet = UiKit.Label(window, "Wallet", null, UiKit.CaptionSize, TextAnchor.MiddleRight);
            listTitle = UiKit.Label(window, "SlotListTitle", "voice.slots", 26);
            openCount = UiKit.Label(window, "OpenSlotCount", null, 22, TextAnchor.MiddleRight);
            Block(window, "HeaderRule", Rule, 0, -108, Width, 1);
            Block(window, "ColumnRule", Rule, 469, -139, 1, 762);

            slotButtons = new Button[VoiceStudioConfig.SlotCount];
            slotGlyphs = new VoiceSlotGlyph[slotButtons.Length];
            slotStatus = new LocalizedText[VoiceStudioConfig.SlotCount];
            lockBadges = new RectTransform[slotButtons.Length];
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int index = i;
                slotButtons[i] = UiKit.Button(window, "Slot" + i, VoiceTextKeys.NameKey((VoiceSlot)i));
                slotGlyphs[i] = Glyph(slotButtons[i].transform, (VoiceSlot)i, 14, -18, 46);
                slotStatus[i] = UiKit.Label(slotButtons[i].transform, "State", null, 22);
                var badge = Block(slotButtons[i].transform, "LockBadge", new Color(.23f, .20f, .15f), ListWidth - 113, -43, 95, 30);
                var badgeText = UiKit.Label(badge, "Label", "voice.lock_badge", 19, TextAnchor.MiddleCenter);
                UiKit.Place(badgeText.transform, .5f, .5f, 0, 0, 89, 30);
                lockBadges[i] = badge;
                slotButtons[i].onClick.AddListener(() => SelectSlot(index));
            }
            slotMarker = Block(window, "SelectedSlotMark", Good, Margin, -193, 4, 64);

            var banner = GoldUiTheme.Rect("StudioIllustration", window);
            UiKit.Place(banner, 0, 1, PanelX, -129, PanelWidth, 239);
            var paper = banner.gameObject.AddComponent<Image>();
            paper.color = GoldUiTheme.Panel;
            paper.raycastTarget = false;
            var plateArt = GoldUiTheme.Rect("Gramophone", banner);
            var print = plateArt.gameObject.AddComponent<RawImage>();
            print.texture = Resources.Load<Texture2D>("Voice/StudioGramophone");
            float aspect = print.texture != null ? (float)print.texture.width / print.texture.height : 3;
            UiKit.Place(plateArt, 1, 1, 0, 0, 239 * aspect, 239);
            print.color = print.texture != null ? Color.white : Surface;
            print.raycastTarget = false;
            illustration = GoldUiInlay.Add(banner, GoldUiInlay.Surface.Illustration);
            slotTitle = UiKit.Label(window, "SlotTitle", null, 36);
            slotDesc = UiKit.Label(window, "SlotDesc", null, UiKit.CaptionSize, TextAnchor.UpperLeft);

            takeButtons = new Button[config.MaxTakes];
            for (int t = 0; t < takeButtons.Length; t++)
            {
                int take = t;
                takeButtons[t] = UiKit.Button(window, "Take" + t, null);
                takeButtons[t].onClick.AddListener(() => SelectTake(take));
            }
            takeMarker = Block(window, "SelectedTakeMark", Good, PanelX, -351, 40, 3);

            waveBox = GoldUiTheme.Rect("WaveBox", window);
            var box = waveBox.gameObject.AddComponent<Image>();
            box.color = new Color(.051f, .060f, .065f);
            box.raycastTarget = false;
            GoldUiInlay.Add(waveBox, GoldUiInlay.Surface.Monitor);
            monitorGlyph = Glyph(waveBox, VoiceSlot.Jump, 26, -43, 105);
            var waveRt = GoldUiTheme.Rect("Wave", waveBox);
            GoldUiTheme.Stretch(waveRt);
            waveRt.offsetMin = new Vector2(158f, 69f);
            waveRt.offsetMax = new Vector2(-28f, -58f);
            wave = waveRt.gameObject.AddComponent<VoiceWaveGraphic>();
            wave.raycastTarget = false;
            wave.color = new Color(.80f, .76f, .65f);
            monitorTitle = UiKit.Label(waveBox, "MonitorTitle", null, 25);
            duration = UiKit.Label(waveBox, "Duration", null, 23, TextAnchor.MiddleRight);
            progressRail = Block(waveBox, "UnlockProgress", Rule, 158, -105, PanelWidth - 186, 6);
            progressFill = Block(progressRail, "Collected", Good, 0, 0, 1, 6);
            countdownText = UiKit.Label(waveBox, "Countdown", null, 110, TextAnchor.MiddleCenter);
            GoldUiTheme.Stretch((RectTransform)countdownText.transform);

            status = UiKit.Label(waveBox, "StatusLine", null, 24);
            Block(window, "ManagementRule", Rule, PanelX, -817, PanelWidth, 1);

            recordButton = UiKit.Button(window, "RecordButton", "voice.record");
            playButton = UiKit.Button(window, "ListenButton", "voice.play");
            deleteButton = UiKit.Button(window, "DeleteButton", "voice.delete");
            useButton = UiKit.Button(window, "UseButton", "voice.use_on");
            closeButton = UiKit.Button(window, "CloseButton", "common.close");
            recordButton.onClick.AddListener(OnRecordPressed);
            playButton.onClick.AddListener(OnListenPressed);
            deleteButton.onClick.AddListener(OnDeletePressed);
            useButton.onClick.AddListener(OnUsePressed);
            closeButton.onClick.AddListener(() => OnCancelRequested());
        }

        private void OnRectTransformDimensionsChange()
        {
            if (window != null)
                UiKit.Fit(window, (RectTransform)transform, Width, Height);
        }

        private static RectTransform Block(Transform parent, string name, Color color, float x, float y, float width, float height)
        {
            var rect = GoldUiTheme.Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            UiKit.Place(rect, 0, 1, x, y, width, height);
            return rect;
        }

        private static VoiceSlotGlyph Glyph(Transform parent, VoiceSlot slot, float x, float y, float size)
        {
            var rect = GoldUiTheme.Rect("SlotSeal", parent);
            UiKit.Place(rect, 0, 1, x, y, size, size);
            var glyph = rect.gameObject.AddComponent<VoiceSlotGlyph>();
            glyph.raycastTarget = false;
            glyph.SetSlot(slot);
            return glyph;
        }

        private static void StyleControl(Button button, Color background, int fontSize)
        {
            // The studio uses flat rows. The button still owns its hover, focus and press feedback.
            GoldUiTheme.Frame(button.transform).enabled = false;
            var colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = new Color(.24f, .22f, .16f);
            colors.selectedColor = new Color(.28f, .24f, .16f);
            colors.disabledColor = new Color(.068f, .071f, .074f);
            button.colors = colors;
            UiKit.TextStyle(button.transform.Find("Label"), fontSize, TextAnchor.MiddleCenter);
        }

        private void Layout()
        {
            UiKit.Fit(window, (RectTransform)transform, Width, Height);
            window.GetComponent<Image>().color = new Color(.071f, .076f, .082f, .99f);
            GoldUiTheme.Frame(window).enabled = false;
            UiKit.Place(title.transform, 0, 1, Margin + 73, -20, 540, 70);
            UiKit.TextStyle(title.transform, 43);
            UiKit.Place(wallet.transform, 1, 1, -225, -30, 540, 48);
            UiKit.TextStyle(wallet.transform, 24, TextAnchor.MiddleRight);
            UiKit.Place(closeButton.transform, 1, 1, -Margin, -29, 134, 51);
            StyleControl(closeButton, Surface, 25);

            UiKit.Place(listTitle.transform, 0, 1, Margin, -133, 270, 45);
            UiKit.TextStyle(listTitle.transform, 27);
            UiKit.Place(openCount.transform, 0, 1, Margin + ListWidth - 108, -133, 108, 45);
            UiKit.TextStyle(openCount.transform, 22, TextAnchor.MiddleRight);
            openCount.GetComponent<Text>().color = UiKit.Muted;
            for (int i = 0; i < slotButtons.Length; i++)
            {
                var row = slotButtons[i];
                UiKit.Place(row.transform, 0, 1, Margin, -190 - i * 89, ListWidth, 83);
                StyleControl(row, Surface, 28);
                var name = row.transform.Find("Label");
                UiKit.Place(name, 0, 1, 78, -5, ListWidth - 94, 41);
                UiKit.TextStyle(name, 28);
                UiKit.Place(slotStatus[i].transform, 0, 1, 78, -46, ListWidth - 94, 31);
                UiKit.TextStyle(slotStatus[i].transform, 21);
                var badgeText = lockBadges[i].GetComponentInChildren<Text>(true);
                UiKit.TextStyle(badgeText.transform, 19, TextAnchor.MiddleCenter);
                badgeText.color = Good;
            }

            UiKit.Place(slotTitle.transform, 0, 1, PanelX + 38, -154, PanelWidth * .56f, 78);
            UiKit.TextStyle(slotTitle.transform, 56);
            UiKit.Place(slotDesc.transform, 0, 1, PanelX + 40, -242, PanelWidth * .53f, 105);
            UiKit.TextStyle(slotDesc.transform, 26, TextAnchor.UpperLeft);

            const float gap = 10;
            float tabWidth = (PanelWidth - gap * (takeButtons.Length - 1)) / takeButtons.Length;
            for (int i = 0; i < takeButtons.Length; i++)
            {
                UiKit.Place(takeButtons[i].transform, 0, 1, PanelX + i * (tabWidth + gap), -383, tabWidth, 56);
                StyleControl(takeButtons[i], Surface, 25);
            }
            UiKit.Place(waveBox, 0, 1, PanelX, -458, PanelWidth, 207);
            UiKit.Place(monitorTitle.transform, 0, 1, 158, -13, PanelWidth - 380, 41);
            UiKit.TextStyle(monitorTitle.transform, 25);
            UiKit.Place(duration.transform, 1, 1, -28, -15, 190, 42);
            UiKit.TextStyle(duration.transform, 23, TextAnchor.MiddleRight);
            duration.GetComponent<Text>().color = UiKit.Muted;
            UiKit.Place(status.transform, 0, 1, 25, -148, PanelWidth - 50, 53);
            UiKit.TextStyle(status.transform, 24);
            UiKit.Place(countdownText.transform, .5f, .5f, 60, 7, 200, 105);
            UiKit.TextStyle(countdownText.transform, 90, TextAnchor.MiddleCenter);

            float recordWidth = PanelWidth * .59f;
            UiKit.Place(recordButton.transform, 0, 1, PanelX, -687, recordWidth, 92);
            UiKit.Place(playButton.transform, 0, 1, PanelX + recordWidth + 16, -687, PanelWidth - recordWidth - 16, 92);
            UiKit.ButtonStyle(recordButton, true);
            UiKit.TextStyle(recordButton.transform.Find("Label"), 36, TextAnchor.MiddleCenter);
            recordButton.transform.Find("Label").GetComponent<Text>().color = GoldUiTheme.PrimaryInk;
            StyleControl(playButton, new Color(.14f, .145f, .15f), 31);
            UiKit.Place(useButton.transform, 0, 1, PanelX, -847, 345, 60);
            UiKit.Place(deleteButton.transform, 1, 1, -Margin, -847, 190, 60);
            StyleControl(useButton, Surface, 25);
            StyleControl(deleteButton, Surface, 24);
        }

        // ------------------------------------------------------------------ state

        private VoiceSlot Slot => (VoiceSlot)slotIndex;
        private VoiceSlotDef Def(int index) => config.Find((VoiceSlot)index);
        private bool Unlocked(int index) { var d = Def(index); return d != null && save.TotalLeaves() >= d.leavesRequired; }
        private int Owned(int index) => progress.OwnedTakes((VoiceSlot)index, config.MaxTakes);

        private bool AnyRecording(int index) => RecordedCount(index) > 0;

        private int RecordedCount(int index)
        {
            int n = 0;
            for (int t = 0; t < Owned(index); t++)
                if (VoiceLibrary.Has((VoiceSlot)index, t)) n++;
            return n;
        }

        private AudioClip SelectedClip => takeIndex < Owned(slotIndex) ? VoiceLibrary.Get(Slot, takeIndex) : null;

        private void SelectSlot(int index)
        {
            if (mode != Mode.Idle)
                return;
            StopPreview();
            if (slotIndex != index) revealTime = 0;
            slotIndex = Mathf.Clamp(index, 0, VoiceStudioConfig.SlotCount - 1);
            takeIndex = 0;
            ClearStatus();
            ShowWave();
            Refresh();
        }

        private void SelectTake(int take)
        {
            if (mode != Mode.Idle)
                return;
            StopPreview();
            takeIndex = Mathf.Clamp(take, 0, takeButtons.Length - 1);
            ClearStatus();
            ShowWave();
            Refresh();
        }

        private void SetStatus(string key, bool warning, params object[] args)
        {
            statusKey = key;
            statusArgs = args;
            statusWarning = warning;
        }

        private void ClearStatus() => statusKey = null;

        /// <summary>Redraws every label and button state from the save, the files on disk and the current mode.</summary>
        private void Refresh()
        {
            int leaves = save.TotalLeaves();
            bool idle = mode == Mode.Idle;
            wallet.SetKey("voice.wallet", leaves, save.totalCoins);
            wallet.GetComponent<Text>().color = UiKit.Muted;

            int openSlots = 0;
            for (int i = 0; i < slotButtons.Length; i++)
            {
                var def = Def(i);
                bool open = def != null && leaves >= def.leavesRequired;
                if (open) openSlots++;
                int recorded = open ? RecordedCount(i) : 0;
                var text = slotStatus[i].GetComponent<Text>();
                lockBadges[i].gameObject.SetActive(!open);
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ListWidth - (open ? 94 : 200));
                if (!open)
                {
                    if (def != null) slotStatus[i].SetKey("voice.list.locked", leaves, def.leavesRequired);
                    else slotStatus[i].SetText(string.Empty);
                    text.color = Locked;
                }
                else if (recorded == 0)
                {
                    slotStatus[i].SetKey("voice.list.default");
                    text.color = UiKit.Muted;
                }
                else if (progress.disabled[i])
                {
                    slotStatus[i].SetKey("voice.list.off");
                    text.color = UiKit.Muted;
                }
                else
                {
                    slotStatus[i].SetKey("voice.list.recorded", recorded, Owned(i));
                    text.color = Good;
                }
                slotButtons[i].interactable = idle;
                var motion = slotButtons[i].GetComponent<GoldUiButton>();
                motion.RestInk = open ? GoldUiTheme.Cream : Locked;
                slotGlyphs[i].SetSlot((VoiceSlot)i, isLocked: !open);
                slotGlyphs[i].color = i == slotIndex ? Good : open ? GoldUiTheme.Cream : Locked;
                GoldUiTheme.MarkLanguage(slotButtons[i], i == slotIndex);
            }
            openCount.SetText($"{openSlots} / {slotButtons.Length}");
            UiKit.Place(slotMarker, 0, 1, Margin, -199 - slotIndex * 89, 4, 65);

            var selected = Def(slotIndex);
            bool unlocked = Unlocked(slotIndex);
            monitorGlyph.SetSlot(Slot, large: true, isLocked: !unlocked);
            monitorGlyph.color = Good;
            int owned = Owned(slotIndex);
            bool takeOwned = takeIndex < owned;
            var clip = unlocked ? SelectedClip : null;

            slotTitle.SetKey(VoiceTextKeys.NameKey((VoiceSlot)slotIndex));
            slotTitle.GetComponent<Text>().color = GoldUiTheme.Cream;
            slotDesc.SetKey(VoiceTextKeys.DescriptionKey((VoiceSlot)slotIndex), selected != null ? selected.maxSeconds.ToString("0.#") : "1");
            slotDesc.GetComponent<Text>().color = UiKit.Muted;

            for (int t = 0; t < takeButtons.Length; t++)
            {
                var label = takeButtons[t].GetComponentInChildren<LocalizedText>(true);
                if (t < owned)
                    label.SetKey(unlocked && VoiceLibrary.Has(Slot, t) ? "voice.take_recorded" : "voice.take", t + 1);
                else
                    label.SetKey("voice.take_locked", t + 1, config.TakePrice(t));
                // Only the next take can be bought; the ones after it wait their turn.
                takeButtons[t].interactable = idle && unlocked && t <= owned;
                GoldUiTheme.MarkLanguage(takeButtons[t], t == takeIndex);
            }
            var activeTab = (RectTransform)takeButtons[takeIndex].transform;
            takeMarker.gameObject.SetActive(unlocked);
            UiKit.Place(takeMarker, 0, 1, activeTab.anchoredPosition.x, -436, activeTab.rect.width, 3);
            progressRail.gameObject.SetActive(!unlocked && selected != null);
            if (!unlocked)
            {
                float fraction = selected != null && selected.leavesRequired > 0 ? Mathf.Clamp01((float)leaves / selected.leavesRequired) : 0;
                progressFill.sizeDelta = new Vector2(progressRail.rect.width * fraction, 6);
                monitorTitle.SetKey("voice.unlock_progress");
                duration.SetText(selected != null ? $"{leaves} / {selected.leavesRequired}" : string.Empty);
            }
            else
            {
                if (!takeOwned) monitorTitle.SetKey("voice.take", takeIndex + 1);
                else if (mode == Mode.Recording) monitorTitle.SetKey("voice.monitor.live");
                else if (previewing) monitorTitle.SetKey("voice.monitor.preview");
                else monitorTitle.SetKey(idle && clip != null ? "voice.monitor.saved" : "voice.monitor.empty");
                float seconds = idle && clip != null ? clip.length : selected.maxSeconds;
                duration.SetKey("voice.duration", seconds.ToString("0.##"));
            }
            wave.gameObject.SetActive(unlocked && takeOwned);

            string recordKey = mode == Mode.Countdown ? "ui.cancel"
                : mode == Mode.Recording ? "voice.stop"
                : !takeOwned ? "voice.buy"
                : clip != null ? "voice.rerecord" : "voice.record";
            recordButton.GetComponentInChildren<LocalizedText>(true).SetKey(recordKey, config.TakePrice(takeIndex));
            recordButton.interactable = unlocked;
            playButton.GetComponentInChildren<LocalizedText>(true).SetKey(previewing ? "voice.stop" : "voice.play");
            playButton.interactable = idle && clip != null;
            deleteButton.interactable = idle && clip != null;
            deleteButton.gameObject.SetActive(clip != null);
            useButton.GetComponentInChildren<LocalizedText>(true).SetKey(progress.disabled[slotIndex] ? "voice.use_off" : "voice.use_on");
            useButton.interactable = idle && unlocked;
            useButton.gameObject.SetActive(unlocked);
            closeButton.interactable = idle;

            countdownText.gameObject.SetActive(mode == Mode.Countdown);
            countdownText.GetComponent<Text>().color = GoldUiTheme.Gold;

            var statusText = status.GetComponent<Text>();
            if (statusKey != null)
            {
                status.SetKey(statusKey, statusArgs);
                statusText.color = statusWarning ? UiKit.Warning : GoldUiTheme.Cream;
            }
            else
            {
                statusText.color = UiKit.Muted;
                if (selected == null) status.SetText(string.Empty);
                else if (!unlocked) status.SetKey("voice.msg.locked", selected.leavesRequired - leaves);
                else if (!takeOwned) status.SetKey("voice.msg.take_hint");
                else if (clip == null) status.SetKey("voice.msg.empty");
                else status.SetKey("voice.msg.ready");
            }
        }

        private void ShowWave()
        {
            var clip = Unlocked(slotIndex) ? SelectedClip : null;
            if (clip == null)
            {
                wave.SetSamples(null);
                return;
            }
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            wave.SetSamples(samples);
        }

        // ------------------------------------------------------------------ actions

        private void OnRecordPressed()
        {
            if (!Unlocked(slotIndex))
                return;
            if (mode == Mode.Countdown) { CancelRecording(); return; }
            if (mode == Mode.Recording) { FinishRecording(); return; }
            if (takeIndex >= Owned(slotIndex)) { BuyTake(); return; }

            StopPreview();
            if (!VoiceRecorder.HasDevice)
            {
                SetStatus("voice.msg.no_mic", true);
                Refresh();
                return;
            }
            mode = Mode.Countdown;
            countdown = config.countdownSeconds;
            systems.Audio.DuckBgm(config.recordBgmDuck, 0.2f);
            wave.SetSamples(null);
            SetStatus("voice.msg.get_ready", false);
            Refresh();
            KeepFocus(recordButton);
        }

        private void BeginRecording()
        {
            if (!recorder.Begin(Def(slotIndex).maxSeconds, config.sampleRate))
            {
                EndRecordingMode();
                SetStatus("voice.msg.mic_failed", true);
                Refresh();
                return;
            }
            mode = Mode.Recording;
            SetStatus("voice.msg.recording", false);
            Refresh();
            KeepFocus(recordButton);
        }

        private void FinishRecording()
        {
            float[] raw = recorder.End(out int rate);
            EndRecordingMode();
            float[] clean = VoiceRecorder.Clean(raw, rate, config, Def(slotIndex).maxSeconds, out bool tooQuiet);
            if (clean == null)
                SetStatus(tooQuiet ? "voice.msg.too_quiet" : "voice.msg.mic_failed", true);
            else if (!VoiceLibrary.Save(Slot, takeIndex, clean, rate))
                SetStatus("voice.msg.save_failed", true);
            else
                SetStatus("voice.msg.saved", false);
            ShowWave();
            if (clean != null && SelectedClip != null)
                StartPreview(); // hear it right away
            Refresh();
        }

        private void CancelRecording()
        {
            recorder.Cancel();
            EndRecordingMode();
            ClearStatus();
            ShowWave();
            Refresh();
        }

        private void EndRecordingMode()
        {
            mode = Mode.Idle;
            systems.Audio.DuckBgm(1f, 0.3f);
        }

        private void BuyTake()
        {
            if (takeIndex != Owned(slotIndex))
                return;
            int price = config.TakePrice(takeIndex);
            if (save.totalCoins < price)
            {
                SetStatus("voice.msg.no_coins", true, price - save.totalCoins);
                Refresh();
                return;
            }
            save.totalCoins -= price;
            progress.extraTakes[slotIndex]++;
            SaveProgress();
            Sfx.Play(SfxId.Special);
            SetStatus("voice.msg.take_unlocked", false);
            Refresh();
        }

        private void OnListenPressed()
        {
            if (mode != Mode.Idle)
                return;
            if (previewing)
                StopPreview();
            else
                StartPreview();
            Refresh();
        }

        private void StartPreview()
        {
            var clip = SelectedClip;
            if (clip == null)
                return;
            previewing = true;
            systems.Audio.DuckBgm(config.previewBgmDuck, 0.15f);
            Voice.Preview(clip);
        }

        private void StopPreview()
        {
            if (!previewing)
                return;
            previewing = false;
            Voice.Stop();
            wave.SetProgress(0f);
            systems.Audio.DuckBgm(1f, 0.3f);
        }

        private void OnDeletePressed()
        {
            if (mode != Mode.Idle || SelectedClip == null)
                return;
            StopPreview();
            VoiceLibrary.Delete(Slot, takeIndex);
            SetStatus("voice.msg.deleted", false);
            ShowWave();
            Refresh();
            KeepFocus(recordButton);
        }

        private void OnUsePressed()
        {
            if (mode != Mode.Idle || !Unlocked(slotIndex))
                return;
            progress.disabled[slotIndex] = !progress.disabled[slotIndex];
            SaveProgress();
            ClearStatus();
            Refresh();
        }

        private void SaveProgress()
        {
            var service = systems.Save;
            if (!service.Save())
                UiFeedback.SaveFailed("ui.save_progress_failed", service.Save);
        }

        private static void KeepFocus(Selectable target)
        {
            if (EventSystem.current != null && target != null && target.IsInteractable())
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        // ------------------------------------------------------------------ tick (PopupBase owns Update for Cancel)

        private void LateUpdate()
        {
            if (!built || IsClosed || systems == null)
                return;
            float dt = Time.unscaledDeltaTime;
            if (revealTime < 1f)
            {
                revealTime = Mathf.Min(1f, revealTime + dt / .50f);
                float ease = Mathf.SmoothStep(0, 1, revealTime);
                illustration.SetReveal(ease);
                monitorGlyph.SetReveal(ease);
                monitorGlyph.transform.localScale = Vector3.one * Mathf.Lerp(.84f, 1, ease);
            }
            switch (mode)
            {
                case Mode.Countdown:
                    countdown -= dt;
                    if (countdown <= 0f)
                    {
                        BeginRecording();
                        break;
                    }
                    int number = Mathf.Clamp(Mathf.CeilToInt(countdown / Mathf.Max(0.01f, config.countdownSeconds) * 3f), 1, 3);
                    countdownText.SetText(number.ToString());
                    break;
                case Mode.Recording:
                    bool over = recorder.Tick(dt);
                    wave.Push(recorder.Elapsed / Def(slotIndex).maxSeconds, recorder.Level * 2.5f);
                    if (!over)
                        break;
                    if (recorder.Failed)
                    {
                        recorder.Cancel();
                        EndRecordingMode();
                        SetStatus("voice.msg.mic_failed", true);
                        ShowWave();
                        Refresh();
                    }
                    else
                        FinishRecording();
                    break;
                default:
                    if (!previewing)
                        break;
                    if (Voice.IsPlaying)
                        wave.SetProgress(Voice.Progress);
                    else
                    {
                        StopPreview();
                        Refresh();
                    }
                    break;
            }
        }

        /// <summary>Esc / B: stops a countdown or a take first; otherwise closes the studio.</summary>
        protected override void OnCancelRequested()
        {
            if (mode != Mode.Idle)
                CancelRecording();
            else
                Close();
        }

        protected override void OnClosed()
        {
            Cleanup();
            onClosed?.Invoke();
        }

        private void OnDisable() => Cleanup(); // also covers a scene unload while recording

        /// <summary>Releases the microphone and gives the BGM and the input mode back. Safe to call twice.</summary>
        private void Cleanup()
        {
            if (cleaned || systems == null)
                return;
            cleaned = true;
            recorder.Cancel();
            Voice.Stop();
            previewing = false;
            mode = Mode.Idle;
            if (systems.Audio != null)
                systems.Audio.DuckBgm(1f, 0.3f);
            if (systems.InputMode != null)
                systems.InputMode.SetMode(previousMode);
        }
    }
}
