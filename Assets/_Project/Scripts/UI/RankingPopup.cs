using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>
    /// Ranking popup (world map). Two tabs: the selected stage's board (best clear time) and the overall board
    /// (cleared stages, then the sum of best times). Shows the top rows and the player's own row.
    /// Without Steam only the player's own record is listed. Built at runtime; no prefab.
    /// </summary>
    public sealed class RankingPopup : PopupBase
    {
        private const float Width = 1180f, Height = 1000f, Margin = 56f;
        private const float RowTop = -318f, RowHeight = 45f;
        private static readonly Color Good = new Color(1f, .82f, .45f);
        private static readonly Color Surface = new Color(.085f, .088f, .091f);
        private static readonly Color Rule = new Color(.58f, .47f, .29f, .50f);

        private sealed class Row
        {
            public RectTransform back;
            public LocalizedText rank, name, extra, value;
        }

        private GameSystems systems;
        private RankingService ranking;
        private Action onClosed;
        private InputMode previousMode;
        private readonly List<StageData> stages = new List<StageData>();

        private RectTransform window, tabMarker;
        private LocalizedText title, stageName, status, selfCaption, network, colRank, colName, colExtra, colValue;
        private Button stageTab, overallTab, prevButton, nextButton, renameButton, closeButton;
        private Row[] rows;
        private Row selfRow;

        private bool overall;
        private int stageIndex;
        private int requestId;

        public static RankingPopup Create(Transform parent)
        {
            var rt = GoldUiTheme.Rect("RankingPopup", parent);
            GoldUiTheme.Stretch(rt);
            rt.gameObject.AddComponent<Image>().color = GoldUiTheme.Dim;
            rt.gameObject.AddComponent<CanvasGroup>();
            return rt.gameObject.AddComponent<RankingPopup>();
        }

        /// <param name="stageId">Stage shown first on the stage tab (the node the player stands on); may be null.</param>
        public void Open(PopupHost host, string stageId, Action closedCallback)
        {
            systems = GameSystems.Instance;
            ranking = systems.Ranking;
            onClosed = closedCallback;
            previousMode = systems.InputMode.Mode;
            systems.InputMode.SetMode(InputMode.UI);

            foreach (var stage in systems.StageDb.Stages)
                if (stage != null)
                    stages.Add(stage);
            stageIndex = Mathf.Max(0, stages.FindIndex(s => s.StageId == stageId));

            Build();
            BeginOpen(host, stageTab.gameObject);
            Layout(); // after the theme pass of BeginOpen, which restyles every text and button
            ShowTab(false);
        }

        // ------------------------------------------------------------------ build / layout

        private void Build()
        {
            window = GoldUiTheme.Rect("Window", transform);
            window.gameObject.AddComponent<Image>().color = GoldUiTheme.Panel;
            title = UiKit.Label(window, "Title", "ranking.title", UiKit.HeadingSize);
            network = UiKit.Label(window, "Network", null, 22, TextAnchor.MiddleRight);
            closeButton = UiKit.Button(window, "CloseButton", "common.close");
            closeButton.onClick.AddListener(Close);
            Block(window, "HeaderRule", Rule, 0, -108, Width, 1);

            stageTab = UiKit.Button(window, "StageTab", "ranking.tab_stage");
            overallTab = UiKit.Button(window, "OverallTab", "ranking.tab_overall");
            stageTab.onClick.AddListener(() => ShowTab(false));
            overallTab.onClick.AddListener(() => ShowTab(true));
            tabMarker = Block(window, "TabMarker", Good, Margin, -186, 300, 3);

            prevButton = UiKit.Button(window, "PrevStage", null);
            nextButton = UiKit.Button(window, "NextStage", null);
            prevButton.GetComponentInChildren<LocalizedText>().SetText("<");
            nextButton.GetComponentInChildren<LocalizedText>().SetText(">");
            prevButton.onClick.AddListener(() => StepStage(-1));
            nextButton.onClick.AddListener(() => StepStage(1));
            stageName = UiKit.Label(window, "StageName", null, 34, TextAnchor.MiddleCenter);

            colRank = UiKit.Label(window, "ColRank", "ranking.col_rank", 22, TextAnchor.MiddleCenter);
            colName = UiKit.Label(window, "ColName", "ranking.col_name", 22);
            colExtra = UiKit.Label(window, "ColExtra", "ranking.col_clears", 22, TextAnchor.MiddleRight);
            colValue = UiKit.Label(window, "ColValue", "ranking.col_time", 22, TextAnchor.MiddleRight);
            Block(window, "ColumnRule", Rule, Margin, RowTop + 6, Width - Margin * 2, 1);

            rows = new Row[ranking.TopCount];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = NewRow("Row" + i, RowTop - i * RowHeight);
            float selfY = RowTop - rows.Length * RowHeight - 46f;
            Block(window, "SelfRule", Rule, Margin, selfY + 40, Width - Margin * 2, 1);
            selfCaption = UiKit.Label(window, "SelfCaption", "ranking.my_record", 21);
            selfRow = NewRow("SelfRow", selfY);
            status = UiKit.Label(window, "Status", null, 28, TextAnchor.MiddleCenter);

            renameButton = UiKit.Button(window, "RenameButton", "ranking.rename");
            renameButton.onClick.AddListener(Rename);
        }

        private Row NewRow(string name, float y)
        {
            var back = Block(window, name, Surface, Margin, y, Width - Margin * 2, RowHeight - 5);
            return new Row
            {
                back = back,
                rank = UiKit.Label(back, "Rank", null, 26, TextAnchor.MiddleCenter),
                name = UiKit.Label(back, "Name", null, 26),
                extra = UiKit.Label(back, "Extra", null, 26, TextAnchor.MiddleRight),
                value = UiKit.Label(back, "Value", null, 26, TextAnchor.MiddleRight),
            };
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

        private static void StyleControl(Button button, int fontSize)
        {
            GoldUiTheme.Frame(button.transform).enabled = false;
            var colors = button.colors;
            colors.normalColor = Surface;
            colors.highlightedColor = new Color(.24f, .22f, .16f);
            colors.selectedColor = new Color(.28f, .24f, .16f);
            colors.disabledColor = new Color(.068f, .071f, .074f);
            button.colors = colors;
            UiKit.TextStyle(button.transform.Find("Label"), fontSize, TextAnchor.MiddleCenter);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (window != null)
                UiKit.Fit(window, (RectTransform)transform, Width, Height);
        }

        private void Layout()
        {
            UiKit.Fit(window, (RectTransform)transform, Width, Height);
            window.GetComponent<Image>().color = new Color(.071f, .076f, .082f, .99f);
            GoldUiTheme.Frame(window).enabled = false;
            float inner = Width - Margin * 2;

            UiKit.Place(title.transform, 0, 1, Margin, -22, 420, 68);
            UiKit.TextStyle(title.transform, 43);
            UiKit.Place(network.transform, 1, 1, -Margin - 150, -32, 420, 46);
            UiKit.TextStyle(network.transform, 22, TextAnchor.MiddleRight);
            UiKit.Place(closeButton.transform, 1, 0, -Margin, 30, 220, 56);
            StyleControl(closeButton, 25);

            UiKit.Place(stageTab.transform, 0, 1, Margin, -128, 300, 58);
            UiKit.Place(overallTab.transform, 0, 1, Margin + 312, -128, 300, 58);
            StyleControl(stageTab, 28);
            StyleControl(overallTab, 28);

            UiKit.Place(prevButton.transform, 0, 1, Margin, -206, 70, 56);
            UiKit.Place(nextButton.transform, 1, 1, -Margin, -206, 70, 56);
            StyleControl(prevButton, 30);
            StyleControl(nextButton, 30);
            UiKit.Place(stageName.transform, .5f, 1, 0, -206, inner - 180, 56);
            UiKit.TextStyle(stageName.transform, 34, TextAnchor.MiddleCenter);

            float headY = RowTop + 44;
            UiKit.Place(colRank.transform, 0, 1, Margin, headY, 120, 34);
            UiKit.Place(colName.transform, 0, 1, Margin + 150, headY, 440, 34);
            UiKit.Place(colExtra.transform, 1, 1, -Margin - 330, headY, 200, 34);
            UiKit.Place(colValue.transform, 1, 1, -Margin - 24, headY, 280, 34);
            foreach (var head in new[] { colRank, colName, colExtra, colValue })
            {
                UiKit.TextStyle(head.transform, 21, head.GetComponent<Text>().alignment);
                head.GetComponent<Text>().color = UiKit.Muted;
            }

            foreach (var row in rows)
                PlaceRow(row);
            PlaceRow(selfRow);
            UiKit.Place(selfCaption.transform, 0, 1, Margin, selfRow.back.anchoredPosition.y + 34, 400, 30);
            UiKit.TextStyle(selfCaption.transform, 21);
            selfCaption.GetComponent<Text>().color = UiKit.Muted;

            UiKit.Place(status.transform, .5f, 1, 0, RowTop - rows.Length * RowHeight * .5f + 40, inner - 80, 80);
            UiKit.TextStyle(status.transform, 28, TextAnchor.MiddleCenter);
            status.GetComponent<Text>().color = UiKit.Muted;

            UiKit.Place(renameButton.transform, 0, 0, Margin, 30, 330, 56);
            StyleControl(renameButton, 25);
        }

        private static void PlaceRow(Row row)
        {
            float w = row.back.sizeDelta.x;
            UiKit.Place(row.rank.transform, 0, .5f, 0, 0, 120, 40);
            UiKit.Place(row.name.transform, 0, .5f, 150, 0, 440, 40);
            UiKit.Place(row.extra.transform, 1, .5f, -330, 0, 200, 40);
            UiKit.Place(row.value.transform, 1, .5f, -24, 0, 280, 40);
            UiKit.TextStyle(row.rank.transform, 26, TextAnchor.MiddleCenter);
            UiKit.TextStyle(row.name.transform, 26);
            UiKit.TextStyle(row.extra.transform, 26, TextAnchor.MiddleRight);
            UiKit.TextStyle(row.value.transform, 26, TextAnchor.MiddleRight);
        }

        // ------------------------------------------------------------------ state

        private void ShowTab(bool showOverall)
        {
            overall = showOverall;
            UiKit.Place(tabMarker, 0, 1, Margin + (overall ? 312 : 0), -186, 300, 3);
            prevButton.gameObject.SetActive(!overall && stages.Count > 1);
            nextButton.gameObject.SetActive(!overall && stages.Count > 1);
            // The stage line doubles as the rule caption of the overall board.
            UiKit.TextStyle(stageName.transform, overall ? 24 : 34, TextAnchor.MiddleCenter);
            stageName.GetComponent<Text>().color = overall ? UiKit.Muted : GoldUiTheme.Cream;
            if (overall)
                stageName.SetKey("ranking.overall_rule");
            colExtra.gameObject.SetActive(overall);
            colValue.SetKey(overall ? "ranking.col_total" : "ranking.col_time");

            bool arrows = prevButton.gameObject.activeSelf;
            stageTab.navigation = Nav(null, arrows ? prevButton : renameButton, overallTab, overallTab);
            overallTab.navigation = Nav(null, arrows ? nextButton : renameButton, stageTab, stageTab);
            prevButton.navigation = Nav(stageTab, renameButton, nextButton, nextButton);
            nextButton.navigation = Nav(overallTab, closeButton, prevButton, prevButton);
            renameButton.navigation = Nav(arrows ? prevButton : stageTab, null, closeButton, closeButton);
            closeButton.navigation = Nav(arrows ? nextButton : overallTab, null, renameButton, renameButton);
            Reload();
        }

        private static Navigation Nav(Selectable up, Selectable down, Selectable left, Selectable right) =>
            new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };

        private void StepStage(int step)
        {
            if (stages.Count == 0)
                return;
            stageIndex = (stageIndex + step + stages.Count) % stages.Count;
            Reload();
        }

        private void Rename()
        {
            if (IsClosed)
                return;
            systems.Popups.OpenNickname(false, changed =>
            {
                if (changed && !IsClosed)
                    Reload();
            });
        }

        private void Reload()
        {
            int id = ++requestId;
            network.SetKey(ranking.Online ? "ranking.online" : "ranking.offline_short");
            network.GetComponent<Text>().color = ranking.Online ? Good : UiKit.Muted;
            foreach (var row in rows)
                row.back.gameObject.SetActive(false);
            Fill(selfRow, default, false);
            status.gameObject.SetActive(true);
            status.SetKey("ranking.loading");

            Action<RankingResult> done = result =>
            {
                if (this == null || IsClosed || id != requestId)
                    return; // closed, or another tab / stage was requested meanwhile
                Show(result);
            };
            if (overall)
                ranking.QueryOverall(done);
            else if (stages.Count > 0)
            {
                stageName.SetKey(stages[stageIndex].DisplayNameKey);
                ranking.QueryStage(stages[stageIndex].StageId, done);
            }
            else
                Show(new RankingResult { status = RankingStatus.Offline });
        }

        private void Show(RankingResult result)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                bool used = i < result.top.Count;
                rows[i].back.gameObject.SetActive(used);
                if (used)
                    Fill(rows[i], result.top[i], true);
            }
            Fill(selfRow, result.self, result.hasSelf);

            if (result.status == RankingStatus.Offline)
                status.SetKey("ranking.offline");
            else if (result.status == RankingStatus.Failed)
                status.SetKey("ranking.failed");
            else if (result.top.Count == 0)
                status.SetKey("ranking.empty");
            status.gameObject.SetActive(result.status != RankingStatus.Ok || result.top.Count == 0);
        }

        private void Fill(Row row, RankingEntry entry, bool has)
        {
            if (!has)
            {
                row.rank.SetText("—");
                row.name.SetKey("ranking.no_record");
                row.extra.SetText(string.Empty);
                row.value.SetText("—");
                Tint(row, UiKit.Muted);
                return;
            }
            row.rank.SetText(entry.rank > 0 ? entry.rank.ToString() : "—");
            row.name.SetText(string.IsNullOrEmpty(entry.name) ? "?" : entry.name);
            if (overall)
            {
                RankingBoards.DecodeOverall(entry.score, out int clears, out float total);
                row.extra.SetText($"{clears} / {stages.Count}");
                row.value.SetText(RankingBoards.FormatTime(total));
            }
            else
            {
                row.extra.SetText(string.Empty);
                row.value.SetText(RankingBoards.FormatTime(entry.score / 1000f));
            }
            Tint(row, entry.isSelf ? Good : GoldUiTheme.Cream);
        }

        private static void Tint(Row row, Color color)
        {
            row.rank.GetComponent<Text>().color = color;
            row.name.GetComponent<Text>().color = color;
            row.extra.GetComponent<Text>().color = color;
            row.value.GetComponent<Text>().color = color;
        }

        protected override void OnClosed()
        {
            requestId++;
            if (GameSystems.Exists)
                GameSystems.Instance.InputMode.SetMode(previousMode);
            onClosed?.Invoke();
        }
    }
}
