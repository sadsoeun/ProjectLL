using System;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Rankings: one board per stage (best clear time) and one overall board (cleared stages, then the sum of best times).
    /// Turns the save into board scores, uploads after a clear and answers the ranking popup.
    /// Without Steam every query still answers with the player's own record (RankingStatus.Offline).
    /// </summary>
    public sealed class RankingService : MonoBehaviour
    {
        [Tooltip("Rows requested for the top list of a board.")]
        [SerializeField, Range(5, 50)] private int topCount = 10;

        private readonly SteamRankingBackend steam = new SteamRankingBackend();
        private SettingsService settings;
        private SaveService save;
        private StageDatabase stages;

        public int TopCount => topCount;
        /// <summary>Steam is running and logged on to its servers: boards can be read and written.</summary>
        public bool Online => steam.Connected;

        private const float ResendInterval = 60f;
        private const int ResendBatch = 2; // two uploads per stage; Steam allows 10 uploads per 10 minutes
        // Stages whose upload did not reach Steam (signed out, request failed). Kept for this run only.
        private readonly System.Collections.Generic.HashSet<string> unsent = new System.Collections.Generic.HashSet<string>();
        private float nextResend;

        public void Initialize(SettingsService settingsService, SaveService saveService, StageDatabase stageDatabase)
        {
            settings = settingsService;
            save = saveService;
            stages = stageDatabase;
            steam.Initialize();
        }

        private void Update()
        {
            steam.Tick();
            if (unsent.Count == 0 || Time.unscaledTime < nextResend)
                return;
            nextResend = Time.unscaledTime + ResendInterval;
            if (!Online)
                return;
            var batch = new System.Collections.Generic.List<string>(unsent);
            for (int i = 0; i < batch.Count && i < ResendBatch; i++)
                ReportClear(batch[i]);
        }

        private void OnDestroy() => steam.Shutdown();

        // ------------------------------------------------------------------ nickname

        public bool NeedsNickname => RankingBoards.CleanNickname(settings.Data.nickname).Length == 0;

        /// <summary>The chosen nickname; before one is chosen, the Steam name (or "Player").</summary>
        public string Nickname
        {
            get
            {
                string name = RankingBoards.CleanNickname(settings.Data.nickname);
                if (name.Length == 0)
                    name = RankingBoards.CleanNickname(steam.PersonaName);
                return name.Length > 0 ? name : "Player";
            }
        }

        /// <summary>Stores the nickname in settings.json. Returns false when it is empty after cleaning or the write failed.</summary>
        public bool SetNickname(string raw)
        {
            string name = RankingBoards.CleanNickname(raw);
            if (name.Length == 0)
                return false;
            settings.Data.nickname = name;
            return settings.Save();
        }

        // ------------------------------------------------------------------ upload

        /// <summary>Call after a stage clear was written to the save: uploads that stage's best time and the overall score.</summary>
        public void ReportClear(string stageId)
        {
            if (save == null || !save.HasSave || save.IsTemporary)
                return;
            var record = save.Current.FindStage(stageId);
            if (record == null || !record.cleared || record.bestTime <= 0f)
                return;
            if (!Online)
            {
                // Signed out of Steam: send it once the connection is back (checked in Update).
                if (steam.Online)
                    unsent.Add(stageId);
                return;
            }
            unsent.Remove(stageId);
            string name = Nickname;
            steam.Submit(RankingBoards.Stage(stageId), true, RankingBoards.StageScore(record.bestTime), name, ok =>
            {
                if (ok) return;
                unsent.Add(stageId);
                Debug.LogWarning("[Ranking] Stage score upload failed: " + stageId);
            });
            RankingBoards.Summarize(save.Current, stages, out int clears, out float total);
            steam.Submit(RankingBoards.Overall, false, RankingBoards.OverallScore(clears, total), name, ok =>
            {
                if (!ok) Debug.LogWarning("[Ranking] Overall score upload failed.");
            });
        }

        // ------------------------------------------------------------------ query

        public void QueryStage(string stageId, Action<RankingResult> done)
        {
            var record = save != null && save.HasSave ? save.Current.FindStage(stageId) : null;
            bool has = record != null && record.cleared && record.bestTime > 0f;
            Query(RankingBoards.Stage(stageId), true, has, has ? RankingBoards.StageScore(record.bestTime) : 0, done);
        }

        public void QueryOverall(Action<RankingResult> done)
        {
            int clears = 0;
            float total = 0f;
            if (save != null && save.HasSave)
                RankingBoards.Summarize(save.Current, stages, out clears, out total);
            Query(RankingBoards.Overall, false, clears > 0, RankingBoards.OverallScore(clears, total), done);
        }

        private void Query(string board, bool ascending, bool hasLocal, int localScore, Action<RankingResult> done)
        {
            if (Online)
            {
                steam.Query(board, ascending, topCount, result =>
                {
                    if (result.status != RankingStatus.Ok)
                        AddLocal(result, hasLocal, localScore);
                    done(result);
                });
                return;
            }
            var offline = new RankingResult { status = RankingStatus.Offline };
            AddLocal(offline, hasLocal, localScore);
            done(offline);
        }

        private void AddLocal(RankingResult result, bool hasLocal, int localScore)
        {
            if (!hasLocal)
                return;
            result.hasSelf = true;
            result.self = new RankingEntry { rank = 0, name = Nickname, score = localScore, isSelf = true };
        }
    }
}
