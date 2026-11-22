#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Steam leaderboards (Steamworks.NET). Owns SteamAPI init / callbacks / shutdown; knows nothing about saves or UI.
    /// Boards are found or created on first use and their handles cached. Scores are uploaded with "keep best",
    /// so a worse run (or a fresh save) never lowers an online record.
    /// Steam limits score uploads to 10 per 10 minutes; the service uploads two per stage clear.
    /// </summary>
    public sealed class SteamRankingBackend
    {
        private const int DetailsMax = 1 + (RankingBoards.NicknameMaxLength + 1) / 2;

        private readonly Dictionary<string, SteamLeaderboard_t> boards = new Dictionary<string, SteamLeaderboard_t>();
        private readonly Dictionary<string, List<Action<SteamLeaderboard_t, bool>>> finding = new Dictionary<string, List<Action<SteamLeaderboard_t, bool>>>();
        // CallResult objects must stay referenced until Steam answers.
        private readonly HashSet<object> pending = new HashSet<object>();

        /// <summary>The Steam client API is initialised (the client is running and the App ID is known).</summary>
        public bool Online { get; private set; }

        /// <summary>
        /// The client is also logged on to the Steam servers. A running client can be signed out or in offline mode;
        /// leaderboard calls fail then, so boards are only used while this is true.
        /// </summary>
        public bool Connected => Online && SteamUser.BLoggedOn();

        public string PersonaName => Online ? SteamFriends.GetPersonaName() : null;

        public bool Initialize()
        {
            Online = false;
            try
            {
                if (!Packsize.Test() || !DllCheck.Test())
                {
                    Debug.LogWarning("[Ranking] Steamworks.NET binaries do not match this platform. Rankings stay offline.");
                    return false;
                }
                // Needs the Steam client running and either a launch through Steam or steam_appid.txt beside the executable / in the project root.
                Online = SteamAPI.Init();
            }
            catch (DllNotFoundException e)
            {
                Debug.LogWarning("[Ranking] steam_api library not found. Rankings stay offline. " + e.Message);
            }
            if (!Online)
                Debug.Log("[Ranking] Steam is not available (client not running or no App ID). Rankings stay offline.");
            return Online;
        }

        public void Tick()
        {
            if (Online)
                SteamAPI.RunCallbacks();
        }

        public void Shutdown()
        {
            if (!Online)
                return;
            Online = false;
            boards.Clear();
            finding.Clear();
            pending.Clear();
            SteamAPI.Shutdown();
        }

        public void Submit(string board, bool ascending, int score, string nickname, Action<bool> done)
        {
            Find(board, ascending, (handle, ok) =>
            {
                if (!ok) { done?.Invoke(false); return; }
                var details = RankingBoards.PackNickname(nickname);
                var call = SteamUserStats.UploadLeaderboardScore(handle, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest, score, details, details.Length);
                Await<LeaderboardScoreUploaded_t>(call, (r, failed) => done?.Invoke(!failed && r.m_bSuccess != 0));
            });
        }

        /// <summary>Top entries plus the local user's own entry.</summary>
        public void Query(string board, bool ascending, int topCount, Action<RankingResult> done)
        {
            var result = new RankingResult { status = RankingStatus.Failed };
            Find(board, ascending, (handle, ok) =>
            {
                if (!ok) { done(result); return; }
                result.totalEntries = SteamUserStats.GetLeaderboardEntryCount(handle);
                var me = SteamUser.GetSteamID();
                var topCall = SteamUserStats.DownloadLeaderboardEntries(handle, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, 1, Mathf.Max(1, topCount));
                Await<LeaderboardScoresDownloaded_t>(topCall, (top, topFailed) =>
                {
                    if (topFailed) { done(result); return; }
                    Read(top, me, result.top);
                    var selfCall = SteamUserStats.DownloadLeaderboardEntriesForUsers(handle, new[] { me }, 1);
                    Await<LeaderboardScoresDownloaded_t>(selfCall, (own, ownFailed) =>
                    {
                        if (!ownFailed)
                        {
                            var list = new List<RankingEntry>();
                            Read(own, me, list);
                            if (list.Count > 0) { result.hasSelf = true; result.self = list[0]; }
                        }
                        result.status = RankingStatus.Ok;
                        done(result);
                    });
                });
            });
        }

        private static void Read(LeaderboardScoresDownloaded_t downloaded, CSteamID me, List<RankingEntry> into)
        {
            var details = new int[DetailsMax];
            for (int i = 0; i < downloaded.m_cEntryCount; i++)
            {
                if (!SteamUserStats.GetDownloadedLeaderboardEntry(downloaded.m_hSteamLeaderboardEntries, i, out var entry, details, DetailsMax))
                    continue;
                string name = RankingBoards.UnpackNickname(details, entry.m_cDetails);
                if (name == null)
                {
                    // No nickname attached: fall back to the Steam name (may still be loading for strangers).
                    SteamFriends.RequestUserInformation(entry.m_steamIDUser, true);
                    name = SteamFriends.GetFriendPersonaName(entry.m_steamIDUser);
                }
                into.Add(new RankingEntry
                {
                    rank = entry.m_nGlobalRank,
                    name = name,
                    score = entry.m_nScore,
                    isSelf = entry.m_steamIDUser == me,
                });
            }
        }

        private void Find(string board, bool ascending, Action<SteamLeaderboard_t, bool> then)
        {
            if (!Online) { then(default, false); return; }
            if (boards.TryGetValue(board, out var known)) { then(known, true); return; }
            if (finding.TryGetValue(board, out var waiting)) { waiting.Add(then); return; }
            finding[board] = new List<Action<SteamLeaderboard_t, bool>> { then };
            var call = SteamUserStats.FindOrCreateLeaderboard(board,
                ascending ? ELeaderboardSortMethod.k_ELeaderboardSortMethodAscending : ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
                ascending ? ELeaderboardDisplayType.k_ELeaderboardDisplayTypeTimeMilliSeconds : ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);
            Await<LeaderboardFindResult_t>(call, (r, failed) =>
            {
                bool ok = !failed && r.m_bLeaderboardFound != 0;
                if (ok)
                    boards[board] = r.m_hSteamLeaderboard;
                else
                    Debug.LogWarning("[Ranking] Could not find or create the board '" + board + "'.");
                if (!finding.TryGetValue(board, out var callbacks))
                    return;
                finding.Remove(board);
                foreach (var cb in callbacks)
                    cb(r.m_hSteamLeaderboard, ok);
            });
        }

        private void Await<T>(SteamAPICall_t call, Action<T, bool> then)
        {
            CallResult<T> handle = null;
            handle = CallResult<T>.Create((value, ioFailure) =>
            {
                pending.Remove(handle);
                if (Online)
                    then(value, ioFailure);
            });
            pending.Add(handle);
            handle.Set(call);
        }
    }
}
#else
using System;

namespace MyGame
{
    /// <summary>Platforms without Steam (web): never online, so the ranking service shows the player's own records only.</summary>
    public sealed class SteamRankingBackend
    {
        public bool Online => false;
        public bool Connected => false;
        public string PersonaName => null;
        public bool Initialize() => false;
        public void Tick() { }
        public void Shutdown() { }
        public void Submit(string board, bool ascending, int score, string nickname, Action<bool> done) => done?.Invoke(false);
        public void Query(string board, bool ascending, int topCount, Action<RankingResult> done) =>
            done(new RankingResult { status = RankingStatus.Offline });
    }
}
#endif
